﻿using System;
using System.Collections.Generic;
using EndlessClient.Handlers;
using EOLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient
{
	public enum WarpAnimation
	{
		None,
		Scroll,
		Admin,
		Invalid = 255
	}

	public class EOMapRenderer : DrawableGameComponent
	{
		public List<MapItem> MapItems { get; set; }
		private List<Character> otherPlayers = new List<Character>();
		public List<NPC> NPCs { get; set; }

		public MapFile MapRef
		{
			get { return _mapRef; }
			set
			{
				_mapRef = value;
				mapNeedsRenderUpdate = true;
			}
		}

		private MapFile _mapRef;
		private RenderTarget2D mapRenderTarget;
		private bool mapNeedsRenderUpdate;

		private SpriteBatch sb;

		public EOMapRenderer(EOGame g, MapFile mapObj)
			: base(g)
		{
			if(g == null)
				throw new NullReferenceException("The game must not be null");

			MapRef = mapObj;
			MapItems = new List<MapItem>();
			NPCs = new List<NPC>();

			sb = new SpriteBatch(Game.GraphicsDevice);
		}

		//super basic implementation for passing on chat to the game's actual HUD
		//map renderer will have to show the speech bubble
		public void RenderChatMessage(Handlers.TalkType messageType, int playerID, string message, ChatType chatType = ChatType.None)
		{
			//convert the messageType into a valid ChatTab to pass everything on to
			ChatTabs tab;
			switch (messageType)
			{
				case TalkType.Local: tab = ChatTabs.Local; break;
				case TalkType.Party: tab = ChatTabs.Group; break;
				default: throw new NotImplementedException();
			}

			//get the character name for the player ID that was received
			string playerName = otherPlayers.Find(x => x.ID == playerID).Name;

			if (EOGame.Instance.Hud == null)
				return;
			EOGame.Instance.Hud.AddChat(tab, playerName, message, chatType);

			//TODO: Add whatever magic is necessary to make chat bubble appear (different colors/transparencies for group and public)
		}

		//renders a chat message from the local mainplayer
		public void RenderLocalChatMessage(string message)
		{
			//show just the speech bubble, since this should be called from the HUD and rendered there already
		}

		public void SetActiveMap(MapFile newActiveMap)
		{
			MapRef = newActiveMap;
			MapItems.Clear();
			otherPlayers.Clear();
			NPCs.Clear();
		}

		public void AddOtherPlayer(Character c, WarpAnimation anim = WarpAnimation.None)
		{
			if(otherPlayers.Find(x => x.Name == c.Name && x.ID == c.ID) == null)
				otherPlayers.Add(c);

			//TODO: Add whatever magic is necessary to make the player appear all pretty (with animation)
		}

		public override void Draw(GameTime gameTime)
		{
			if (MapRef != null)
			{
				_drawImmutableMapObjects();
			}

			sb.Begin();
			sb.Draw(mapRenderTarget, new Vector2(0, 0), Color.White);
			sb.End();

			base.Draw(gameTime);
		}

		private void _drawImmutableMapObjects()
		{
			if(mapRenderTarget == null)
				mapRenderTarget = new RenderTarget2D(Game.GraphicsDevice, 
					Game.GraphicsDevice.PresentationParameters.BackBufferWidth, 
					Game.GraphicsDevice.PresentationParameters.BackBufferHeight,
					false,
					Game.GraphicsDevice.PresentationParameters.BackBufferFormat,
					DepthFormat.Depth24);

			if (!mapNeedsRenderUpdate) return;

			mapNeedsRenderUpdate = false;
			Game.GraphicsDevice.SetRenderTarget(mapRenderTarget);

			


			Game.GraphicsDevice.SetRenderTarget(null);
		}
	}
}