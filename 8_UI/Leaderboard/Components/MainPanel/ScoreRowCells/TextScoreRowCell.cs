﻿using System;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;
using TMPro;

namespace BeatLeader.Components {
    internal class TextScoreRowCell : AbstractScoreRowCell {
        #region Setup

        private Func<object, string> _formatter;

        public void Setup(
            Func<object, string> formatter,
            TextAlignmentOptions alignmentOption = TextAlignmentOptions.Center,
            TextOverflowModes overflowMode = TextOverflowModes.Overflow,
            float fontSize = 3.4f
        ) {
            _formatter = formatter;
            textComponent.alignment = alignmentOption;
            textComponent.overflowMode = overflowMode;
            textComponent.fontSize = fontSize;
        }

        #endregion

        #region Implementation

        public void SetValue(object value) {
            textComponent.text = value == null ? "" : _formatter.Invoke(value);
            IsEmpty = false;
        }

        public override void SetAlpha(float value) {
            textComponent.alpha = value;
        }

        protected override float CalculatePreferredWidth() {
            return textComponent.preferredWidth;
        }

        #endregion

        #region TextComponent

        [UIComponent("text-component"), UsedImplicitly]
        public TextMeshProUGUI textComponent;

        #endregion
    }
}