﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EndlessClient.Dialogs;
using EOLib;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.Net;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XNAControls;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Color = Microsoft.Xna.Framework.Color;

namespace EndlessClient.HUD
{
	//This is going to be a single item in the inventory. will handle it's own drag/drop, onmouseover, etc.
	public class EOInventoryItem : XNAControl
	{
		private readonly ItemRecord m_itemData;
		public ItemRecord ItemData
		{
			get { return m_itemData; }
		}

		private InventoryItem m_inventory;
		public InventoryItem Inventory { get { return m_inventory; } set { m_inventory = value; } }

		public int Slot { get; private set; }

		private readonly Texture2D m_itemgfx, m_highlightBG;
		private XNALabel m_nameLabel;

		private bool m_beingDragged;
		private int m_alpha = 255;
		private int m_preDragDrawOrder;
		private XNAControl m_preDragParent;
		private int m_oldOffX, m_oldOffY;
		public bool Dragging
		{
			get { return m_beingDragged; }
		}

		private int m_recentClickCount;
		private readonly Timer m_recentClickTimer;

		private readonly PacketAPI m_api;
		private static bool safetyCommentHasBeenShown;

		public EOInventoryItem(PacketAPI api, int slot, ItemRecord itemData, InventoryItem itemInventoryInfo, EOInventory inventory)
			: base(null, null, inventory)
		{
			m_api = api;
			m_itemData = itemData;
			m_inventory = itemInventoryInfo;
			Slot = slot;

			UpdateItemLocation(Slot);

			m_itemgfx = ((EOGame)Game).GFXManager.TextureFromResource(GFXTypes.Items, 2 * itemData.Graphic, true);

			m_highlightBG = new Texture2D(Game.GraphicsDevice, DrawArea.Width - 3, DrawArea.Height - 3);
			Color[] highlight = new Color[(drawArea.Width - 3) * (drawArea.Height - 3)];
			for (int i = 0; i < highlight.Length; ++i) { highlight[i] = Color.FromNonPremultiplied(200, 200, 200, 60); }
			m_highlightBG.SetData(highlight);
			
			_initItemLabel();

			m_recentClickTimer = new Timer(
				_state => { if (m_recentClickCount > 0) Interlocked.Decrement(ref m_recentClickCount); }, null, 0, 1000);
		}

		public override void Update(GameTime gameTime)
		{
			if (!Game.IsActive || !Enabled) return;

			//check for drag-drop here
			MouseState currentState = Mouse.GetState();

			if (!m_beingDragged && MouseOverPreviously && MouseOver && PreviousMouseState.LeftButton == ButtonState.Pressed && currentState.LeftButton == ButtonState.Pressed)
			{
				//Conditions for starting are the mouse is over, the button is pressed, and no other items are being dragged
				if (((EOInventory) parent).NoItemsDragging())
				{
					//start the drag operation and hide the item label
					m_beingDragged = true;
					m_nameLabel.Visible = false;
					m_preDragDrawOrder = DrawOrder;
					m_preDragParent = parent;
					
					//make sure the offsets are maintained!
					//required to enable dragging past bounds of the inventory panel
					m_oldOffX = xOff;
					m_oldOffY = yOff;
					SetParent(null);
					
					m_alpha = 128;
					DrawOrder = 200; //arbitrarily large constant so drawn on top while dragging
				}
			}

			if (m_beingDragged && PreviousMouseState.LeftButton == ButtonState.Pressed &&
			    currentState.LeftButton == ButtonState.Pressed)
			{
				//dragging has started. continue dragging until mouse is released, update position based on mouse location
				DrawLocation = new Vector2(currentState.X - (DrawArea.Width/2), currentState.Y - (DrawArea.Height/2));
			}
			else if (m_beingDragged && PreviousMouseState.LeftButton == ButtonState.Pressed &&
			         currentState.LeftButton == ButtonState.Released)
			{
				//need to check for: drop on map (drop action)
				//					 drop on button junk/drop
				//					 drop on grid (inventory move action)
				//					 drop on [x] dialog ([x] add action)

				m_alpha = 255;
				SetParent(m_preDragParent);

				if (((EOInventory) parent).IsOverDrop() || (World.Instance.ActiveMapRenderer.MouseOver && 
					ChestDialog.Instance == null && EOPaperdollDialog.Instance == null && LockerDialog.Instance == null
					&& BankAccountDialog.Instance == null && TradeDialog.Instance == null))
				{
					Point loc = World.Instance.ActiveMapRenderer.MouseOver ? World.Instance.ActiveMapRenderer.GridCoords:
						new Point(World.Instance.MainPlayer.ActiveCharacter.X, World.Instance.MainPlayer.ActiveCharacter.Y);

					//in range if maximum coordinate difference is <= 2 away
					bool inRange = Math.Abs(Math.Max(World.Instance.MainPlayer.ActiveCharacter.X - loc.X, World.Instance.MainPlayer.ActiveCharacter.Y - loc.Y)) <= 2;

					if (m_itemData.Special == ItemSpecial.Lore)
					{
						EOMessageBox.Show(DATCONST1.ITEM_IS_LORE_ITEM, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
					}
					else if (World.Instance.JailMap == World.Instance.MainPlayer.ActiveCharacter.CurrentMap)
					{
						EOMessageBox.Show(World.GetString(DATCONST2.JAIL_WARNING_CANNOT_DROP_ITEMS),
							World.GetString(DATCONST2.STATUS_LABEL_TYPE_WARNING),
							XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
					}
					else if (m_inventory.amount > 1 && inRange)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.DropItems,
							m_inventory.amount);
						dlg.DialogClosing += (sender, args) =>
						{
							if (args.Result == XNADialogResult.OK)
							{
								//note: not sure of the actual limit. 10000 is arbitrary here
								if (dlg.SelectedAmount > 10000 && m_inventory.id == 1 && !safetyCommentHasBeenShown)
									EOMessageBox.Show(DATCONST1.DROP_MANY_GOLD_ON_GROUND, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader,
										(o, e) => { safetyCommentHasBeenShown = true; });
								else if (!m_api.DropItem(m_inventory.id, dlg.SelectedAmount, (byte) loc.X, (byte) loc.Y))
									((EOGame) Game).DoShowLostConnectionDialogAndReturnToMainMenu();
							}
						};
					}
					else if (inRange)
					{
						if (!m_api.DropItem(m_inventory.id, 1, (byte)loc.X, (byte)loc.Y))
							((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();
					}
					else /*if (!inRange)*/
					{
						EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_WARNING, DATCONST2.STATUS_LABEL_ITEM_DROP_OUT_OF_RANGE);
					}
				}
				else if (((EOInventory) parent).IsOverJunk())
				{
					if (m_inventory.amount > 1)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.JunkItems,
							m_inventory.amount, DATCONST2.DIALOG_TRANSFER_JUNK);
						dlg.DialogClosing += (sender, args) =>
						{
							if (args.Result == XNADialogResult.OK && !m_api.JunkItem(m_inventory.id, dlg.SelectedAmount))
								((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();
						};
					}
					else if (!m_api.JunkItem(m_inventory.id, 1))
						((EOGame) Game).DoShowLostConnectionDialogAndReturnToMainMenu();
				}
				else if (ChestDialog.Instance != null && ChestDialog.Instance.MouseOver && ChestDialog.Instance.MouseOverPreviously)
				{
					if (m_itemData.Special == ItemSpecial.Lore)
					{
						EOMessageBox.Show(DATCONST1.ITEM_IS_LORE_ITEM, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
					}
					else if (m_inventory.amount > 1)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.DropItems, m_inventory.amount);
						dlg.DialogClosing += (sender, args) =>
						{
							if (args.Result == XNADialogResult.OK &&
							    !m_api.ChestAddItem(ChestDialog.Instance.CurrentChestX, ChestDialog.Instance.CurrentChestY,
								    m_inventory.id, dlg.SelectedAmount))
								EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
						};
					}
					else
					{
						if (!m_api.ChestAddItem(ChestDialog.Instance.CurrentChestX, ChestDialog.Instance.CurrentChestY, m_inventory.id, 1))
							EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
					}
				}
				else if (EOPaperdollDialog.Instance != null && EOPaperdollDialog.Instance.MouseOver && EOPaperdollDialog.Instance.MouseOverPreviously)
				{
					//equipable items should be equipped
					//other item types should do nothing
					switch (m_itemData.Type)
					{
						case ItemType.Accessory:
						case ItemType.Armlet:
						case ItemType.Armor:
						case ItemType.Belt:
						case ItemType.Boots:
						case ItemType.Bracer:
						case ItemType.Gloves:
						case ItemType.Hat:
						case ItemType.Necklace:
						case ItemType.Ring:
						case ItemType.Shield:
						case ItemType.Weapon:
							_handleDoubleClick();
							break;
					}
				}
				else if (LockerDialog.Instance != null && LockerDialog.Instance.MouseOver && LockerDialog.Instance.MouseOverPreviously)
				{
					byte x = LockerDialog.Instance.X;
					byte y = LockerDialog.Instance.Y;
					if (m_inventory.id == 1)
					{
						EOMessageBox.Show(DATCONST1.LOCKER_DEPOSIT_GOLD_ERROR, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
					}
					else if (m_inventory.amount > 1)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.ShopTransfer, m_inventory.amount, DATCONST2.DIALOG_TRANSFER_TRANSFER);
						dlg.DialogClosing += (sender, args) =>
						{
							if (args.Result == XNADialogResult.OK)
							{
								if (LockerDialog.Instance.GetNewItemAmount(m_inventory.id, dlg.SelectedAmount) > Constants.LockerMaxSingleItemAmount)
									EOMessageBox.Show(DATCONST1.LOCKER_FULL_SINGLE_ITEM_MAX, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
								else if (!m_api.LockerAddItem(x, y, m_inventory.id, dlg.SelectedAmount))
									EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
							}
						};
					}
					else
					{
						if (LockerDialog.Instance.GetNewItemAmount(m_inventory.id, 1) > Constants.LockerMaxSingleItemAmount)
							EOMessageBox.Show(DATCONST1.LOCKER_FULL_SINGLE_ITEM_MAX, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
						else if (!m_api.LockerAddItem(x, y, m_inventory.id, 1))
							EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
					}
				}
				else if (BankAccountDialog.Instance != null && BankAccountDialog.Instance.MouseOver && BankAccountDialog.Instance.MouseOverPreviously && m_inventory.id == 1)
				{
					if (m_inventory.amount == 0)
					{
						EOMessageBox.Show(DATCONST1.BANK_ACCOUNT_UNABLE_TO_DEPOSIT, XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
					}
					else if (m_inventory.amount > 1)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.BankTransfer,
							m_inventory.amount, DATCONST2.DIALOG_TRANSFER_DEPOSIT);
						dlg.DialogClosing += (o, e) =>
						{
							if (e.Result == XNADialogResult.Cancel)
								return;

							if (!m_api.BankDeposit(dlg.SelectedAmount))
								EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
						};
					}
					else
					{
						if (!m_api.BankDeposit(1))
							EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
					}
				}
				else if (TradeDialog.Instance != null && TradeDialog.Instance.MouseOver && TradeDialog.Instance.MouseOverPreviously
					&& !TradeDialog.Instance.MainPlayerAgrees)
				{
					if (m_itemData.Special == ItemSpecial.Lore)
					{
						EOMessageBox.Show(DATCONST1.ITEM_IS_LORE_ITEM);
					}
					else if (m_inventory.amount > 1)
					{
						ItemTransferDialog dlg = new ItemTransferDialog(m_itemData.Name, ItemTransferDialog.TransferType.TradeItems,
							m_inventory.amount, DATCONST2.DIALOG_TRANSFER_OFFER);
						dlg.DialogClosing += (o, e) =>
						{
							if (e.Result != XNADialogResult.OK) return;

							if (!m_api.TradeAddItem(m_inventory.id, dlg.SelectedAmount))
							{
								TradeDialog.Instance.Close(XNADialogResult.NO_BUTTON_PRESSED);
								((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();
							}
						};
					}
					else if(!m_api.TradeAddItem(m_inventory.id, 1))
					{
						TradeDialog.Instance.Close(XNADialogResult.NO_BUTTON_PRESSED);
						((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();
					}
				}

				//update the location - if it isn't on the grid, the bounds check will set it back to where it used to be originally
				//Item amount will be updated or item will be removed in packet response to the drop operation
				UpdateItemLocation(ItemCurrentSlot());

				//mouse has been released. finish dragging.
				m_beingDragged = false;
				m_nameLabel.Visible = true;
				DrawOrder = m_preDragDrawOrder;
			}

			if (!m_beingDragged && PreviousMouseState.LeftButton == ButtonState.Pressed &&
			    currentState.LeftButton == ButtonState.Released && MouseOver && MouseOverPreviously)
			{
				Interlocked.Increment(ref m_recentClickCount);
				if (m_recentClickCount == 2)
				{
					_handleDoubleClick();
				}
			}

			if (!MouseOverPreviously && MouseOver && !m_beingDragged)
			{
				m_nameLabel.Visible = true;
				EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_ITEM, m_nameLabel.Text);
			}
			else if (!MouseOver && !m_beingDragged && m_nameLabel != null && m_nameLabel.Visible)
			{
				m_nameLabel.Visible = false;
			}

			base.Update(gameTime); //sets mouseoverpreviously = mouseover, among other things
		}

		public override void Draw(GameTime gameTime)
		{
			if (!Visible) return;

			SpriteBatch.Begin();
			if (MouseOver)
			{
				int currentSlot = ItemCurrentSlot();
				Vector2 drawLoc = m_beingDragged
					? new Vector2(m_oldOffX + 13 + 26*(currentSlot%EOInventory.INVENTORY_ROW_LENGTH),
						m_oldOffY + 9 + 26*(currentSlot/EOInventory.INVENTORY_ROW_LENGTH)) //recalculate the top-left point for the highlight based on the current drag position
					: new Vector2(DrawAreaWithOffset.X, DrawAreaWithOffset.Y);

				if (EOInventory.GRID_AREA.Contains(DrawAreaWithOffset))
					SpriteBatch.Draw(m_highlightBG, drawLoc, Color.White);
			}
			if(m_itemgfx != null)
				SpriteBatch.Draw(m_itemgfx, new Vector2(DrawAreaWithOffset.X, DrawAreaWithOffset.Y), Color.FromNonPremultiplied(255, 255, 255, m_alpha));
			SpriteBatch.End();
			base.Draw(gameTime);
		}

		private void UpdateItemLocation(int newSlot)
		{
			if (Slot != newSlot && ((EOInventory) parent).MoveItem(this, newSlot)) Slot = newSlot;

			//top-left grid slot in the inventory is 115, 339
			//parent top-left is 103, 330
			//grid size is 26*26 (w/o borders 23*23)
			int width, height;
			EOInventory._getItemSizeDeltas(m_itemData.Size, out width, out height);
			DrawLocation = new Vector2(13 + 26 * (Slot % EOInventory.INVENTORY_ROW_LENGTH), 9 + 26 * (Slot / EOInventory.INVENTORY_ROW_LENGTH));
			_setSize(width * 26, height * 26);

			if (m_nameLabel != null) //fix the position of the name label too if we aren't creating the inventoryitem
			{
				m_nameLabel.DrawLocation = new Vector2(DrawArea.Width, 0);
				if(!EOInventory.GRID_AREA.Contains(m_nameLabel.DrawAreaWithOffset))
					m_nameLabel.DrawLocation = new Vector2(-m_nameLabel.DrawArea.Width, 0); //show on the right if it isn't in bounds!
				m_nameLabel.ResizeBasedOnText(16, 9);
			}
		}

		private int ItemCurrentSlot()
		{
			if (!m_beingDragged) return Slot;

			//convert the current draw area to a slot number (for when the item is dragged)
			return (int)((DrawLocation.X - m_oldOffX - 13)/26) + EOInventory.INVENTORY_ROW_LENGTH * (int)((DrawLocation.Y - m_oldOffY - 9)/26);
		}

		public void UpdateItemLabel()
		{
			m_nameLabel.Text = GetNameString(m_inventory.id, m_inventory.amount);
			m_nameLabel.ResizeBasedOnText(16, 9);
		}

		protected override void Dispose(bool disposing)
		{
			if(m_recentClickTimer != null) m_recentClickTimer.Dispose();
			if(m_nameLabel != null) m_nameLabel.Dispose();
			if(m_highlightBG != null) m_highlightBG.Dispose();

			base.Dispose(disposing);
		}

		private void _initItemLabel()
		{
			if (m_nameLabel != null) m_nameLabel.Dispose();

			m_nameLabel = new XNALabel(new Rectangle(DrawArea.Width, 0, 150, 23), Constants.FontSize08)
			{
				Visible = false,
				AutoSize = false,
				TextAlign = LabelAlignment.MiddleCenter,
				ForeColor = Color.FromNonPremultiplied(200, 200, 200, 255),
				BackColor = Color.FromNonPremultiplied(30, 30, 30, 160)
			};
			
			UpdateItemLabel();

			m_nameLabel.ForeColor = GetItemTextColor((short)m_itemData.ID);

			m_nameLabel.SetParent(this);
			m_nameLabel.ResizeBasedOnText(16, 9);
		}

		private void _handleDoubleClick()
		{
			Character c = World.Instance.MainPlayer.ActiveCharacter;

			bool useItem = false;
			switch (m_itemData.Type)
			{
			//equippable items
				case ItemType.Accessory:
				case ItemType.Armlet:
				case ItemType.Armor:
				case ItemType.Belt:
				case ItemType.Boots:
				case ItemType.Bracer:
				case ItemType.Gloves:
				case ItemType.Hat:
				case ItemType.Necklace:
				case ItemType.Ring:
				case ItemType.Shield:
				case ItemType.Weapon:
					byte subLoc = 0;
					if (m_itemData.Type == ItemType.Armlet || m_itemData.Type == ItemType.Ring || m_itemData.Type == ItemType.Bracer)
					{
						if (c.PaperDoll[(int) m_itemData.GetEquipLocation()] == 0)
							subLoc = 0;
						else if (c.PaperDoll[(int) m_itemData.GetEquipLocation() + 1] == 0)
							subLoc = 1;
						else
						{
							EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_INFORMATION,
								DATCONST2.STATUS_LABEL_ITEM_EQUIP_TYPE_ALREADY_EQUIPPED);
							break;
						}
					}
					else if (m_itemData.Type == ItemType.Armor &&
					         m_itemData.Gender != c.RenderData.gender)
					{
						EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_INFORMATION,
							DATCONST2.STATUS_LABEL_ITEM_EQUIP_DOES_NOT_FIT_GENDER);
						break;
					}
					
					//check stat requirements
					int[] reqs = new int[6];
					string[] reqNames = {"STR", "INT", "WIS", "AGI", "CON", "CHA"};
					if ((reqs[0] = m_itemData.StrReq) > c.Stats.Str || (reqs[1] = m_itemData.IntReq) > c.Stats.Int
				     || (reqs[2] = m_itemData.WisReq) > c.Stats.Wis || (reqs[3] = m_itemData.AgiReq) > c.Stats.Agi
				     || (reqs[4] = m_itemData.ConReq) > c.Stats.Con || (reqs[5] = m_itemData.ChaReq) > c.Stats.Cha)
					{
						int reqIndex = reqs.ToList().FindIndex(x => x > 0);

						((EOGame) Game).Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_INFORMATION,
							DATCONST2.STATUS_LABEL_ITEM_EQUIP_THIS_ITEM_REQUIRES,
							string.Format(" {0} {1}", reqs[reqIndex], reqNames[reqIndex]));
						break;
					}
					//check class requirement
					if (m_itemData.ClassReq > 0 && m_itemData.ClassReq != c.Class)
					{
						((EOGame) Game).Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_INFORMATION,
							DATCONST2.STATUS_LABEL_ITEM_EQUIP_CAN_ONLY_BE_USED_BY,
							((ClassRecord) World.Instance.ECF.Data.Find(x => ((ClassRecord) x).ID == m_itemData.ClassReq)).Name);
						break;
					}

					if (c.EquipItem(m_itemData.Type, (short) m_itemData.ID,
						(short) m_itemData.DollGraphic))
					{
						if (!m_api.EquipItem((short) m_itemData.ID, subLoc))
							EOGame.Instance.DoShowLostConnectionDialogAndReturnToMainMenu();
					}
					else
						EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_INFORMATION,
							DATCONST2.STATUS_LABEL_ITEM_EQUIP_TYPE_ALREADY_EQUIPPED);

					break;
			//usable items
				case ItemType.Teleport:
					if (!World.Instance.ActiveMapRenderer.MapRef.CanScroll)
					{
						EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_ACTION, DATCONST2.STATUS_LABEL_NOTHING_HAPPENED);
						break;
					}
					if (m_itemData.ScrollMap == World.Instance.MainPlayer.ActiveCharacter.CurrentMap &&
					    m_itemData.ScrollX == World.Instance.MainPlayer.ActiveCharacter.X &&
					    m_itemData.ScrollY == World.Instance.MainPlayer.ActiveCharacter.Y)
						break; //already there - no need to scroll!
					useItem = true;
					break;
				case ItemType.Heal:
				case ItemType.HairDye:
				case ItemType.Beer:
					useItem = true;
					break;
				case ItemType.CureCurse:
					//note: don't actually set the useItem bool here. Call API.UseItem if the dialog result is OK.
					if (c.PaperDoll.Select(id => World.Instance.EIF.GetItemRecordByID(id))
						.Any(rec => rec.Special == ItemSpecial.Cursed)) //only do the use if the player has a cursed item equipped
					{
						EOMessageBox.Show(DATCONST1.ITEM_CURSE_REMOVE_PROMPT, XNADialogButtons.OkCancel, EOMessageBoxStyle.SmallDialogSmallHeader,
							(o, e) =>
							{
								if (e.Result == XNADialogResult.OK && !m_api.UseItem((short)m_itemData.ID))
								{
									((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();
								}
							});
					}
					break;
				case ItemType.EXPReward:
					useItem = true;
					break;
				case ItemType.EffectPotion:
					useItem = true;
					break;
				//Not implemented server-side
				//case ItemType.SkillReward:
				//	break;
				//case ItemType.StatReward:
				//	break;
			}

			if (useItem && !m_api.UseItem((short) m_itemData.ID))
				((EOGame)Game).DoShowLostConnectionDialogAndReturnToMainMenu();

			m_recentClickCount = 0;
		}

		public static Color GetItemTextColor(short id) //also used in map renderer for mapitems
		{
			ItemRecord data = World.Instance.EIF.GetItemRecordByID(id);
			switch (data.Special)
			{
				case ItemSpecial.Lore:
				case ItemSpecial.Unique:
					return Color.FromNonPremultiplied(0xff, 0xf0, 0xa5, 0xff);
				case ItemSpecial.Rare:
					return Color.FromNonPremultiplied(0xf5, 0xc8, 0x9c, 0xff);
			}

			return Color.White;
		}

		public static string GetNameString(short id, int amount)
		{
			ItemRecord data = World.Instance.EIF.GetItemRecordByID(id);
			switch (data.ID)
			{
				case 1:
					return string.Format("{0} {1}", amount, data.Name);
				default:
					if (amount == 1)
						return data.Name;
					if (amount > 1)
						return string.Format("{0} x{1}", data.Name, amount);
					throw new Exception("There shouldn't be an item in the inventory with amount zero");
			}
		}
	}

	public class EOInventory : XNAControl
	{
		/// <summary>
		/// number of slots in an inventory row
		/// </summary>
		public const int INVENTORY_ROW_LENGTH = 14;

		/// <summary>
		/// Area of the grid portion of the inventory (uses absolute coordinates)
		/// </summary>
		public static Rectangle GRID_AREA = new Rectangle(110, 334, 377, 116);

		private readonly bool[,] m_filledSlots = new bool[4, INVENTORY_ROW_LENGTH]; //4 rows, 14 columns = 56 total in grid
		private readonly RegistryKey m_inventoryKey;
		private readonly List<EOInventoryItem> m_childItems = new List<EOInventoryItem>();

		private readonly XNALabel m_lblWeight;
		private readonly XNAButton m_btnDrop, m_btnJunk, m_btnPaperdoll;

		private readonly PacketAPI m_api;
		
		public EOInventory(XNAPanel parent, PacketAPI api)
			: base(null, null, parent)
		{
			m_api = api;

			//load info from registry
			Dictionary<int, int> localItemSlotMap = new Dictionary<int, int>();
			m_inventoryKey = _tryGetCharacterRegKey();
			if (m_inventoryKey != null)
			{
				const string itemFmt = "item{0}";
				for (int i = 0; i < INVENTORY_ROW_LENGTH * 4; ++i)
				{
					int id;
					try
					{
						id = Convert.ToInt32(m_inventoryKey.GetValue(string.Format(itemFmt, i)));
					}
					catch { continue; }
					localItemSlotMap.Add(i, id);
				}
			}

			//add the inventory items that were retrieved from the server
			List<InventoryItem> localInv = World.Instance.MainPlayer.ActiveCharacter.Inventory;
			if (localInv.Find(_item => _item.id == 1).id != 1)
				localInv.Insert(0, new InventoryItem {amount = 0, id = 1}); //add 0 gold if there isn't any gold

			bool dialogShown = false;
			foreach (InventoryItem item in localInv)
			{
				ItemRecord rec = World.Instance.EIF.GetItemRecordByID(item.id);
				int slot = localItemSlotMap.ContainsValue(item.id)
					? localItemSlotMap.First(_pair => _pair.Value == item.id).Key
					: _getNextOpenSlot(rec.Size);

				List<Tuple<int, int>> points;
				if (!_fitsInSlot(slot, rec.Size, out points))
					slot = _getNextOpenSlot(rec.Size);

				if (!_addItemToSlot(slot, rec, item.amount) && !dialogShown)
				{
					dialogShown = true;
					EOMessageBox.Show("Something doesn't fit in the inventory. Rearrange items or get rid of them.", "Warning", XNADialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
				}
			}

			//coordinates for parent of EOInventory: 102, 330 (pnlInventory in InGameHud)
			//extra in photoshop right now: 8, 31

			//current weight label (member variable, needs to have text updated when item amounts change)
			m_lblWeight = new XNALabel(new Rectangle(385, 37, 88, 18), Constants.FontSize08pt5)
			{
				ForeColor = Constants.LightGrayText,
				TextAlign = LabelAlignment.MiddleCenter,
				Visible = true,
				AutoSize = false
			};
			m_lblWeight.SetParent(this);
			UpdateWeightLabel();

			Texture2D thatWeirdSheet = ((EOGame)Game).GFXManager.TextureFromResource(GFXTypes.PostLoginUI, 27); //oh my gawd the offsets on this bish

			//(local variables, added to child controls)
			//'paperdoll' button
			m_btnPaperdoll = new XNAButton(thatWeirdSheet, new Vector2(385, 9), /*new Rectangle(39, 385, 88, 19)*/null, new Rectangle(126, 385, 88, 19));
			m_btnPaperdoll.SetParent(this);
			m_btnPaperdoll.OnClick += (s, e) => m_api.RequestPaperdoll((short)World.Instance.MainPlayer.ActiveCharacter.ID);
			//'drop' button
			//491, 398 -> 389, 68
			//0,15,38,37
			//0,52,38,37
			m_btnDrop = new XNAButton(thatWeirdSheet, new Vector2(389, 68), new Rectangle(0, 15, 38, 37), new Rectangle(0, 52, 38, 37));
			m_btnDrop.SetParent(this);
			World.IgnoreDialogs(m_btnDrop);
			//'junk' button - 4 + 38 on the x away from drop
			m_btnJunk = new XNAButton(thatWeirdSheet, new Vector2(431, 68), new Rectangle(0, 89, 38, 37), new Rectangle(0, 126, 38, 37));
			m_btnJunk.SetParent(this);
			World.IgnoreDialogs(m_btnJunk);
		}

		//-----------------------------------------------------
		// Overrides / Control Interface
		//-----------------------------------------------------
		public override void Update(GameTime gameTime)
		{
			if (!Visible || !Game.IsActive) return;

			if (IsOverDrop())
			{
				EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_BUTTON, DATCONST2.STATUS_LABEL_INVENTORY_DROP_BUTTON);
			}
			else if (IsOverJunk())
			{
				EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_BUTTON, DATCONST2.STATUS_LABEL_INVENTORY_JUNK_BUTTON);
			}
			else if (m_btnPaperdoll.MouseOver && !m_btnPaperdoll.MouseOverPreviously)
			{
				EOGame.Instance.Hud.SetStatusLabel(DATCONST2.STATUS_LABEL_TYPE_BUTTON, DATCONST2.STATUS_LABEL_INVENTORY_SHOW_YOUR_PAPERDOLL);
			}

			base.Update(gameTime);
		}

		protected override void Dispose(bool disposing)
		{
			m_inventoryKey.Dispose();
			m_btnPaperdoll.Dispose();
			m_btnJunk.Dispose();
			m_btnDrop.Dispose();
		}

		private bool _addItemToSlot(int slot, ItemRecord item, int count = 1)
		{
			return _addItemToSlot(m_filledSlots, slot, item, count);
		}

		private bool _addItemToSlot(bool[,] filledSlots, int slot, ItemRecord item, int count = 1)
		{
			//this is ADD item - don't allow adding items that have been added already
			if (slot < 0 || m_childItems.Count(_item => _item.Slot == slot) > 0) return false;
			
			List<Tuple<int, int>> points;
			if (!_fitsInSlot(filledSlots, slot, item.Size, out points)) return false;
			points.ForEach(point => filledSlots[point.Item1, point.Item2] = true); //flag that the spaces are taken

			m_inventoryKey.SetValue(string.Format("item{0}", slot), item.ID, RegistryValueKind.String); //update the registry
			m_childItems.Add(new EOInventoryItem(m_api, slot, item, new InventoryItem { amount = count, id = (short)item.ID }, this)); //add the control wrapper for the item
			m_childItems[m_childItems.Count - 1].DrawOrder = (int) ControlDrawLayer.DialogLayer - (2 + slot%INVENTORY_ROW_LENGTH);
			return true;
		}

		public bool ItemFits(short id)
		{
			//it will fit if the inventory already has it.
			if (m_childItems.Find(_i => _i.ItemData.ID == id) != null)
				return true;

			ItemRecord rec = World.Instance.EIF.GetItemRecordByID(id);
			int nextSlot = _getNextOpenSlot(rec.Size);
			List<Tuple<int, int>> dummy;
			return _fitsInSlot(nextSlot, rec.Size, out dummy);
		}

		/// <summary>
		/// Checks if a list of Item IDs will fit in the inventory based on their item record sizes. Does not modify current inventory.
		/// </summary>
		/// <param name="newItems">List of Items to check</param>
		/// <param name="oldItems">Optional list of items to remove from filled slots before checking new IDs (ie items that will be traded)</param>
		/// <returns>True if everything fits, false otherwise.</returns>
		public bool ItemsFit(List<InventoryItem> newItems, List<InventoryItem> oldItems = null)
		{
			bool[,] tempFilledSlots = new bool[4, INVENTORY_ROW_LENGTH];
			for (int row = 0; row < 4; ++row)
			{
				for (int col = 0; col < INVENTORY_ROW_LENGTH; ++col)
				{
					tempFilledSlots[row, col] = m_filledSlots[row, col];
				}
			}

			if(oldItems != null)
				foreach (InventoryItem item in oldItems)
				{
					EOInventoryItem control = m_childItems.Find(_item => _item.ItemData.ID == item.id);
					if (control != null && control.Inventory.amount - item.amount <= 0)
						_unmarkItemSlots(tempFilledSlots, _getTakenSlots(control.Slot, control.ItemData.Size));
				}

			foreach (InventoryItem item in newItems)
			{
				if (oldItems != null && oldItems.FindIndex(_itm => _itm.id == item.id) < 0)
				{
					if (item.id == 1 || m_childItems.Find(_item => _item.ItemData.ID == item.id) != null)
						continue; //already in inventory: skip, since it isn't a new item
				}

				ItemRecord rec = World.Instance.EIF.GetItemRecordByID(item.id);

				int nextSlot = _getNextOpenSlot(tempFilledSlots, rec.Size);
				List<Tuple<int, int>> points;
				if (nextSlot < 0 || !_fitsInSlot(tempFilledSlots, nextSlot, rec.Size, out points))
					return false;

				foreach (var pt in points)
					tempFilledSlots[pt.Item1, pt.Item2] = true;
			}
			return true;
		}

		private void _unmarkItemSlots(bool[,] slots, List<Tuple<int, int>> points)
		{
			points.ForEach(_p => slots[_p.Item1, _p.Item2] = false);
		}

		private void _removeItemFromSlot(int slot, int count = 1)
		{
			EOInventoryItem control = m_childItems.Find(_control => _control.Slot == slot);
			if (control == null || slot < 0) return;

			int numLeft = control.Inventory.amount - count;

			if (numLeft <= 0 && control.Inventory.id != 1)
			{
				ItemSize sz = control.ItemData.Size;
				_unmarkItemSlots(m_filledSlots, _getTakenSlots(control.Slot, sz));

				m_inventoryKey.SetValue(string.Format("item{0}", slot), 0, RegistryValueKind.String);
				m_childItems.Remove(control);
				control.Visible = false;
				control.Close();
			}
			else
			{
				control.Inventory = new InventoryItem {amount = numLeft, id = control.Inventory.id};
				control.UpdateItemLabel();
			}
		}

		public bool MoveItem(EOInventoryItem childItem, int newSlot)
		{
			if (childItem.Slot == newSlot) return true; // We did it, Reddit!

			List<Tuple<int, int>> oldPoints = _getTakenSlots(childItem.Slot, childItem.ItemData.Size);
			List<Tuple<int, int>> points;
			if (!_fitsInSlot(newSlot, childItem.ItemData.Size, out points, oldPoints)) return false;

			oldPoints.ForEach(_p => m_filledSlots[_p.Item1, _p.Item2] = false);
			points.ForEach(_p => m_filledSlots[_p.Item1, _p.Item2] = true);

			m_inventoryKey.SetValue(string.Format("item{0}", childItem.Slot), 0, RegistryValueKind.String);
			m_inventoryKey.SetValue(string.Format("item{0}", newSlot), childItem.ItemData.ID, RegistryValueKind.String);

			childItem.DrawOrder = (int)ControlDrawLayer.DialogLayer - (2 + childItem.Slot % INVENTORY_ROW_LENGTH);

			return true;
		}

		private int _getNextOpenSlot(ItemSize size)
		{
			return _getNextOpenSlot(m_filledSlots, size);
		}

		private int _getNextOpenSlot(bool[,] slots, ItemSize size)
		{
			int width, height;
			_getItemSizeDeltas(size, out width, out height);

			//outer loops: iterating over every grid space (56 spaces total)
			for (int row = 0; row < 4; ++row)
			{
				for (int col = 0; col < INVENTORY_ROW_LENGTH; ++col)
				{
					if (slots[row, col]) continue;

					if (!slots[row, col] && size == ItemSize.Size1x1)
						return row*INVENTORY_ROW_LENGTH + col;

					//inner loops: iterating over grid spaces starting at (row, col) for the item size (width, height)
					bool ok = true;
					for (int y = row; y < row + height; ++y)
					{
						if (y >= 4)
						{
							ok = false;
							continue;
						}
						for (int x = col; x < col + width; ++x)
							if (x >= INVENTORY_ROW_LENGTH || slots[y, x]) ok = false;
					}

					if (ok) return row*INVENTORY_ROW_LENGTH + col;
				}
			}

			return -1;
		}

		public void UpdateWeightLabel(string text = "")
		{
			if (string.IsNullOrEmpty(text))
				m_lblWeight.Text = string.Format("{0} / {1}", World.Instance.MainPlayer.ActiveCharacter.Weight,
					World.Instance.MainPlayer.ActiveCharacter.MaxWeight);
			else
				m_lblWeight.Text = text;
		}

		public bool NoItemsDragging()
		{
			return m_childItems.Count(invItem => invItem.Dragging) == 0;
		}

		public bool UpdateItem(InventoryItem item)
		{
			EOInventoryItem ctrl;
			if((ctrl = m_childItems.Find(_ctrl => _ctrl.ItemData.ID == item.id)) != null)
			{
				ctrl.Inventory = item;
				ctrl.UpdateItemLabel();
			}
			else
			{
				ItemRecord rec = World.Instance.EIF.GetItemRecordByID(item.id);
				return _addItemToSlot(_getNextOpenSlot(rec.Size), rec, item.amount);
			}

			return true;
		}

		public void RemoveItem(int id)
		{
			EOInventoryItem ctrl;
			if ((ctrl = m_childItems.Find(_ctrl => _ctrl.ItemData.ID == id)) != null)
			{
				_removeItemFromSlot(ctrl.Slot, ctrl.Inventory.amount);
			}
		}

		public bool IsOverDrop()
		{
			return m_btnDrop.MouseOver && m_btnDrop.MouseOverPreviously;
		}

		public bool IsOverJunk()
		{
			return m_btnJunk.MouseOver && m_btnJunk.MouseOverPreviously;
		}

		//-----------------------------------------------------
		// Helper methods
		//-----------------------------------------------------
		private static RegistryKey _tryGetCharacterRegKey()
		{	
			try
			{
				using (RegistryKey currentUser = Registry.CurrentUser)
				{
					return currentUser.CreateSubKey(string.Format("Software\\EndlessClient\\{0}\\{1}\\inventory", World.Instance.MainPlayer.AccountName, World.Instance.MainPlayer.ActiveCharacter.Name),
						RegistryKeyPermissionCheck.ReadWriteSubTree);
				}
			}
			catch (NullReferenceException) { }
			return null;
		}

		private static List<Tuple<int, int>> _getTakenSlots(int slot, ItemSize sz)
		{
			var ret = new List<Tuple<int, int>>();

			int width, height;
			_getItemSizeDeltas(sz, out width, out height);
			int y = slot / INVENTORY_ROW_LENGTH, x = slot % INVENTORY_ROW_LENGTH;
			for (int row = y; row < height + y; ++row)
			{
				for (int col = x; col < width + x; ++col)
				{
					ret.Add(new Tuple<int, int>(row, col));
				}
			}

			return ret;
		}

		private bool _fitsInSlot(int slot, ItemSize sz, out List<Tuple<int, int>> points, List<Tuple<int, int>> itemPoints = null)
		{
			return _fitsInSlot(m_filledSlots, slot, sz, out points, itemPoints);
		}

		private bool _fitsInSlot(bool[,] slots, int slot, ItemSize sz, out List<Tuple<int, int>> points, List<Tuple<int, int>> itemPoints = null)
		{
			points = new List<Tuple<int, int>>();

			if (slot < 0 || slot >= 4*INVENTORY_ROW_LENGTH) return false;

			//check the 'filled slots' array to see if the item can go in 'slot' based on its size
			int y = slot / INVENTORY_ROW_LENGTH, x = slot % INVENTORY_ROW_LENGTH;
			if (y >= 4 || x >= INVENTORY_ROW_LENGTH) return false;
			if (itemPoints == null && slots[y, x]) return false;

			points = _getTakenSlots(slot, sz);
			if (points.Count(_t => _t.Item1 < 0 || _t.Item1 > 3 || _t.Item2 < 0 || _t.Item2 >= INVENTORY_ROW_LENGTH) > 0)
				return false; //some of the coordinates are out of bounds of the maximum inventory length

			List<Tuple<int, int>> overLaps = points.FindAll(_pt => slots[_pt.Item1, _pt.Item2]);
			if (overLaps.Count > 0 && (itemPoints == null || overLaps.Count(itemPoints.Contains) != overLaps.Count))
				return false; //more than one overlapping point, and the points in overLaps are not contained in itemPoints

			return true;
		}

		//this is public because C# doesn't have a 'friend' keyword and I need it in EOInventoryItem
		public static void _getItemSizeDeltas(ItemSize size, out int width, out int height)
		{
			//enum ItemSize: Size[width]x[height], 
			//	where [width] is index 4 and [height] is index 6 (string of length 7)
			string sizeStr = Enum.GetName(typeof(ItemSize), size);
			if (sizeStr == null || sizeStr.Length != 7)
			{
				width = height = 0;
				return;
			}

			width = Convert.ToInt32(sizeStr.Substring(4, 1));
			height = Convert.ToInt32(sizeStr.Substring(6, 1));
		}

		public void EnableEffectPotions()
		{
			var effectPotions = m_childItems.Where(x => x.ItemData.Type == ItemType.EffectPotion && !x.Enabled);
			foreach (var potion in effectPotions)
				potion.Enabled = true;
		}

		public void DisableEffectPotions()
		{
			var effectPotions = m_childItems.Where(x => x.ItemData.Type == ItemType.EffectPotion && x.Enabled);
			foreach (var potion in effectPotions)
				potion.Enabled = false;
		}
	}
}