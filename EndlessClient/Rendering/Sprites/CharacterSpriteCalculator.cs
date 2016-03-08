﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using EOLib;
using EOLib.Data.BLL;
using EOLib.Graphics;
using EOLib.Net.API;
using Microsoft.Xna.Framework;

namespace EndlessClient.Rendering.Sprites
{
	public class CharacterSpriteCalculator : ICharacterSpriteCalculator
	{
		private readonly INativeGraphicsManager _gfxManager;
		private readonly ICharacterRenderProperties _characterRenderProperties;

		public CharacterSpriteCalculator(INativeGraphicsManager gfxManager,
										 ICharacterRenderProperties characterRenderProperties)
		{
			_gfxManager = gfxManager;
			_characterRenderProperties = characterRenderProperties;
		}

		public ISpriteSheet GetBootsTexture(bool isBow)
		{
			BootsSpriteType type = BootsSpriteType.Standing;
			switch (_characterRenderProperties.CurrentAction)
			{
				case CharacterActionState.Walking:
					switch (_characterRenderProperties.WalkFrame)
					{
						case 1: type = BootsSpriteType.WalkFrame1; break;
						case 2: type = BootsSpriteType.WalkFrame2; break;
						case 3: type = BootsSpriteType.WalkFrame3; break;
						case 4: type = BootsSpriteType.WalkFrame4; break;
					}
					break;
				case CharacterActionState.Attacking:
					if (!isBow && _characterRenderProperties.AttackFrame == 2 ||
						isBow && _characterRenderProperties.AttackFrame == 1)
						type = BootsSpriteType.Attack;
					break;
				case CharacterActionState.Sitting:
					switch (_characterRenderProperties.SitState)
					{
						case SitState.Chair: type = BootsSpriteType.SitChair; break;
						case SitState.Floor: type = BootsSpriteType.SitGround; break;
					}
					break;
			}

			var gfxFile = _characterRenderProperties.Gender == 0 ? GFXTypes.FemaleShoes : GFXTypes.MaleShoes;

			var offset = GetOffsetBasedOnState(type) * GetBaseOffsetFromDirection();
			var baseBootGraphic = GetBaseBootGraphic();
			var gfxNumber = baseBootGraphic + (int)type + offset;

			return new SpriteSheet(_gfxManager.TextureFromResource(gfxFile, gfxNumber, true));
		}

		public ISpriteSheet GetArmorTexture(bool isBow)
		{
			ArmorShieldSpriteType type = ArmorShieldSpriteType.Standing;
			switch (_characterRenderProperties.CurrentAction)
			{
				case CharacterActionState.Walking:
					switch (_characterRenderProperties.WalkFrame)
					{
						case 1: type = ArmorShieldSpriteType.WalkFrame1; break;
						case 2: type = ArmorShieldSpriteType.WalkFrame2; break;
						case 3: type = ArmorShieldSpriteType.WalkFrame3; break;
						case 4: type = ArmorShieldSpriteType.WalkFrame4; break;
					}
					break;
				case CharacterActionState.Attacking:
					if (isBow)
					{
						switch (_characterRenderProperties.AttackFrame)
						{
							case 1: type = ArmorShieldSpriteType.Bow; break;
							case 2: type = ArmorShieldSpriteType.Standing; break;
						}
					}
					else
					{
						switch (_characterRenderProperties.AttackFrame)
						{
							case 1: type = ArmorShieldSpriteType.PunchFrame1; break;
							case 2: type = ArmorShieldSpriteType.PunchFrame2; break;
						}
					}
					break;
				case CharacterActionState.SpellCast:
					type = ArmorShieldSpriteType.SpellCast;
					break;
				case CharacterActionState.Sitting:
					switch (_characterRenderProperties.SitState)
					{
						case SitState.Chair:
							type = ArmorShieldSpriteType.SitChair;
							break;
						case SitState.Floor:
							type = ArmorShieldSpriteType.SitGround;
							break;
					}
					break;
			}

			var gfxFile = _characterRenderProperties.Gender == 0 ? GFXTypes.FemaleArmor : GFXTypes.MaleArmor;

			var offset = GetOffsetBasedOnState(type) * GetBaseOffsetFromDirection();
			var baseArmorValue = GetBaseArmorGraphic();
			var gfxNumber = baseArmorValue + (int)type + offset;

			return new SpriteSheet(_gfxManager.TextureFromResource(gfxFile, gfxNumber, true));
		}

		public ISpriteSheet GetHatTexture()
		{
			var gfxFile = _characterRenderProperties.Gender == 0 ? GFXTypes.FemaleHat : GFXTypes.MaleHat;

			var offset = 2 * GetBaseOffsetFromDirection();
			int baseHatValue = GetBaseHatGraphic();
			int gfxNumber = baseHatValue + 1 + offset;

			return new SpriteSheet(_gfxManager.TextureFromResource(gfxFile, gfxNumber, true));
		}

		public ISpriteSheet GetShieldTexture(bool shieldIsOnBack)
		{
			//front shields have one size gfx, back arrows/wings have another size.

			var type = ArmorShieldSpriteType.Standing;
			int offset = GetBaseOffsetFromDirection();

			if (!shieldIsOnBack)
			{
				if (_characterRenderProperties.CurrentAction == CharacterActionState.Walking)
				{
					switch (_characterRenderProperties.WalkFrame)
					{
						case 1: type = ArmorShieldSpriteType.WalkFrame1; break;
						case 2: type = ArmorShieldSpriteType.WalkFrame2; break;
						case 3: type = ArmorShieldSpriteType.WalkFrame3; break;
						case 4: type = ArmorShieldSpriteType.WalkFrame4; break;
					}
				}
				else if (_characterRenderProperties.CurrentAction == CharacterActionState.Attacking)
				{
					switch (_characterRenderProperties.AttackFrame)
					{
						case 1: type = ArmorShieldSpriteType.PunchFrame1; break;
						case 2: type = ArmorShieldSpriteType.PunchFrame2; break;
					}
				}
				else if (_characterRenderProperties.CurrentAction == CharacterActionState.SpellCast)
				{
					type = ArmorShieldSpriteType.SpellCast;
				}
				else if(_characterRenderProperties.CurrentAction == CharacterActionState.Sitting)
				{
					return null;
				}

				offset *= GetOffsetBasedOnState(type);
			}
			else
			{
				//different gfx numbering scheme for shield items worn on the back:
				//	Standing = 1/2
				//	Attacking = 3/4
				//	Extra = 5 (unused?)
				if (_characterRenderProperties.CurrentAction == CharacterActionState.Attacking &&
					_characterRenderProperties.AttackFrame == 1)
					type = ArmorShieldSpriteType.ShieldItemOnBack_AttackingWithBow;
			}

			var gfxFile = _characterRenderProperties.Gender == 0 ? GFXTypes.FemaleBack : GFXTypes.MaleBack;

			var baseShieldValue = GetBaseShieldGraphic();
			var gfxNumber = baseShieldValue + (int)type + offset;
			return new SpriteSheet(_gfxManager.TextureFromResource(gfxFile, gfxNumber, true));
		}

		public ISpriteSheet GetWeaponTexture(bool isBow)
		{
			var type = WeaponSpriteType.Standing;

			switch (_characterRenderProperties.CurrentAction)
			{
				case CharacterActionState.Walking:
					switch (_characterRenderProperties.WalkFrame)
					{
						case 1: type = WeaponSpriteType.WalkFrame1; break;
						case 2: type = WeaponSpriteType.WalkFrame2; break;
						case 3: type = WeaponSpriteType.WalkFrame3; break;
						case 4: type = WeaponSpriteType.WalkFrame4; break;
					}
					break;
				case CharacterActionState.Attacking:
					if (isBow)
					{
						switch (_characterRenderProperties.AttackFrame)
						{
							case 1: type = WeaponSpriteType.Shooting; break;
							case 2: type = WeaponSpriteType.Standing; break;
						}
					}
					else
					{
						switch (_characterRenderProperties.AttackFrame)
						{
							case 1: type = WeaponSpriteType.SwingFrame1; break;
							case 2:
								type = _characterRenderProperties.Direction == EODirection.Down
									|| _characterRenderProperties.Direction == EODirection.Right
									? WeaponSpriteType.SwingFrame2Spec : WeaponSpriteType.SwingFrame2;
								break;
						}
					}
					break;
				case CharacterActionState.SpellCast:
					type = WeaponSpriteType.SpellCast;
					break;
				case CharacterActionState.Sitting:
					return null; //no weapon when sitting
			}

			var gfxFile = _characterRenderProperties.Gender == 0 ? GFXTypes.FemaleWeapons : GFXTypes.MaleWeapons;

			var offset = GetOffsetBasedOnState(type) * GetBaseOffsetFromDirection();
			var baseWeaponValue = GetBaseWeaponGraphic();
			var gfxNumber = baseWeaponValue + (int)type + offset;

			return new SpriteSheet(_gfxManager.TextureFromResource(gfxFile, gfxNumber, true));
		}

		public ISpriteSheet GetSkinTexture(bool isBow)
		{
			var sheetRows = 7;
			var sheetColumns = 4;
			var gfxNum = 1;

			if (_characterRenderProperties.CurrentAction == CharacterActionState.Walking && _characterRenderProperties.WalkFrame > 0)
			{
				gfxNum = 2;
				sheetColumns = 16;
			}
			else if (_characterRenderProperties.CurrentAction == CharacterActionState.Attacking && _characterRenderProperties.AttackFrame > 0)
			{
				if (!isBow)
				{
					gfxNum = 3;
					sheetColumns = 8;
				}
				else if (_characterRenderProperties.AttackFrame == 1) //only 1 frame of bow/gun animation
				{
					gfxNum = 7; //4 columns in this one too
				}
			}
			else if (_characterRenderProperties.CurrentAction == CharacterActionState.SpellCast)
			{
				gfxNum = 4;
			}
			else if (_characterRenderProperties.CurrentAction == CharacterActionState.Sitting)
			{
				if (_characterRenderProperties.SitState == SitState.Floor) gfxNum = 6;
				else if (_characterRenderProperties.SitState == SitState.Chair) gfxNum = 5;
			}
			//similar if statements for spell, emote, etc

			var texture = _gfxManager.TextureFromResource(GFXTypes.SkinSprites, gfxNum, true);

			var rotated = _characterRenderProperties.Direction == EODirection.Left ||
						  _characterRenderProperties.Direction == EODirection.Up;
			var heightDelta = texture.Height / sheetRows; //the height of one 'row' in the sheet
			var widthDelta = texture.Width / sheetColumns; //the width of one 'column' in the sheet
			var section = texture.Width / 4; //each 'section' for a different set of graphics

			var walkExtra = _characterRenderProperties.WalkFrame > 0 ? widthDelta * (_characterRenderProperties.WalkFrame - 1) : 0;
			walkExtra = !isBow && _characterRenderProperties.AttackFrame > 0 ? widthDelta * (_characterRenderProperties.AttackFrame - 1) : walkExtra;

			var sourceArea = new Rectangle(
				_characterRenderProperties.Gender * widthDelta * (sheetColumns / 2) + (rotated ? section : 0) + walkExtra,
				_characterRenderProperties.Race * heightDelta,
				widthDelta,
				heightDelta);

			return new SpriteSheet(texture, sourceArea);
		}

		public ISpriteSheet GetHairTexture()
		{
			throw new System.NotImplementedException();
		}

		public ISpriteSheet GetFaceTexture()
		{
			throw new System.NotImplementedException();
		}

		public ISpriteSheet GetEmoteTexture()
		{
			if (_characterRenderProperties.EmoteFrame < 0 ||
				_characterRenderProperties.Emote == Emote.Trade ||
				_characterRenderProperties.Emote == Emote.LevelUp)
			{
				return null;
			}

			//14 rows (7 female - 7 male) / 11 columns
			const int ROWS = 14;
			const int COLS = 11;

			var texture = _gfxManager.TextureFromResource(GFXTypes.SkinSprites, 8, true);

			var widthDelta = texture.Width / COLS;
			var heightDelta = texture.Height / ROWS;
			var genderOffset = texture.Height / 2 * _characterRenderProperties.Gender;
			//'playful' is the last face in the gfx (ndx 10), even though it has enum value of 14 (ndx 13)
			var emote = _characterRenderProperties.Emote == Emote.Playful ||
						_characterRenderProperties.Emote == Emote.Drunk
						? 10 : (int)_characterRenderProperties.Emote - 1;

			var sourceRectangle = new Rectangle(widthDelta * emote, heightDelta * _characterRenderProperties.Race + genderOffset, widthDelta, heightDelta);

			return new SpriteSheet(texture, sourceRectangle);
		}

		private short GetBaseBootGraphic()
		{
			return (short) ((_characterRenderProperties.BootsGraphic - 1)*40);
		}

		private short GetBaseArmorGraphic()
		{
			return (short) ((_characterRenderProperties.ArmorGraphic - 1)*50);
		}

		private short GetBaseHatGraphic()
		{
			return (short)((_characterRenderProperties.HatGraphic - 1) * 10);
		}

		private short GetBaseShieldGraphic()
		{
			return (short)((_characterRenderProperties.ShieldGraphic - 1) * 50);
		}

		private short GetBaseWeaponGraphic()
		{
			return (short)((_characterRenderProperties.WeaponGraphic - 1) * 100);
		}

		private int GetBaseOffsetFromDirection()
		{
			return _characterRenderProperties.Direction == EODirection.Down ||
				   _characterRenderProperties.Direction  == EODirection.Right ? 0 : 1;
		}

		private int GetOffsetBasedOnState(BootsSpriteType type)
		{
			switch (type)
			{
				case BootsSpriteType.WalkFrame1:
				case BootsSpriteType.WalkFrame2:
				case BootsSpriteType.WalkFrame3:
				case BootsSpriteType.WalkFrame4:
					return 4;
			}
			return 1;
		}

		private int GetOffsetBasedOnState(ArmorShieldSpriteType type)
		{
			switch (type)
			{
				case ArmorShieldSpriteType.WalkFrame1:
				case ArmorShieldSpriteType.WalkFrame2:
				case ArmorShieldSpriteType.WalkFrame3:
				case ArmorShieldSpriteType.WalkFrame4:
					return 4;
				case ArmorShieldSpriteType.PunchFrame1:
				case ArmorShieldSpriteType.PunchFrame2:
					return 2;
			}
			return 1;
		}

		private int GetOffsetBasedOnState(WeaponSpriteType type)
		{
			switch (type)
			{
				case WeaponSpriteType.WalkFrame1:
				case WeaponSpriteType.WalkFrame2:
				case WeaponSpriteType.WalkFrame3:
				case WeaponSpriteType.WalkFrame4:
					return 4;
				case WeaponSpriteType.SwingFrame1:
				case WeaponSpriteType.SwingFrame2:
					return 2;
			}
			return 1;
		}
	}
}