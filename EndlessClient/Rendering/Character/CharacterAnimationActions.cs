﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using EndlessClient.ControlSets;
using EndlessClient.HUD.Controls;
using EOLib;
using EOLib.Domain.Character;

namespace EndlessClient.Rendering.Character
{
    public class CharacterAnimationActions : ICharacterAnimationActions, IOtherCharacterAnimationNotifier
    {
        private readonly IHudControlProvider _hudControlProvider;
        private readonly ICharacterRepository _characterRepository;

        public CharacterAnimationActions(IHudControlProvider hudControlProvider,
                                         ICharacterRepository characterRepository)
        {
            _hudControlProvider = hudControlProvider;
            _characterRepository = characterRepository;
        }

        public void Face(EODirection direction)
        {
            var renderProperties = _characterRepository.MainCharacter.RenderProperties;
            renderProperties = renderProperties.WithDirection(direction);

            var newMainCharacter = _characterRepository.MainCharacter.WithRenderProperties(renderProperties);
            _characterRepository.MainCharacter = newMainCharacter;
        }

        public void StartWalking()
        {
            if (!_hudControlProvider.IsInGame)
                return;

            var animator = _hudControlProvider.GetComponent<ICharacterAnimator>(HudControlIdentifier.CharacterAnimator);
            animator.StartMainCharacterWalkAnimation();
        }

        public void StartOtherCharacterWalkAnimation(int characterID)
        {
            if (!_hudControlProvider.IsInGame)
                return;

            var animator = _hudControlProvider.GetComponent<ICharacterAnimator>(HudControlIdentifier.CharacterAnimator);
            animator.StartOtherCharacterWalkAnimation(characterID);
        }
    }

    public interface ICharacterAnimationActions
    {
        void Face(EODirection direction);

        void StartWalking();
    }
}
