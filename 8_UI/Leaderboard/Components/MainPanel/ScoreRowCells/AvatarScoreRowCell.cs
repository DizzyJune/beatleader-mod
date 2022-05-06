﻿using BeatLeader.Models;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;

namespace BeatLeader.Components {
    internal class AvatarScoreRowCell : AbstractScoreRowCell {
        #region Components

        private const float Size = 4.0f;

        [UIValue("player-avatar"), UsedImplicitly]
        private PlayerAvatar _playerAvatar;

        private void Awake() {
            _playerAvatar = Instantiate<PlayerAvatar>(transform);
        }

        #endregion

        #region Implementation

        public void SetValues(string url, PlayerRole playerRole) {
            _playerAvatar.SetAvatar(url, playerRole);
            IsEmpty = false;
        }

        public override void SetAlpha(float value) {
            _playerAvatar.SetAlpha(value);
        }

        protected override float CalculatePreferredWidth() {
            return Size;
        }

        #endregion
    }
}