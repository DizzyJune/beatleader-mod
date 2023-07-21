﻿using System;
using HMUI;
using IPA.Utilities;
using UnityEngine;
using Screen = HMUI.Screen;

namespace BeatLeader.Components {
    internal class DummyViewController : ViewController {
        public static DummyViewController Wrap(ViewController controller, bool deactivateAfterTransition = false) {
            var dummy = controller.gameObject.AddComponent<DummyViewController>();
            dummy.Init(controller);
            dummy.deactivateAfterTransition = deactivateAfterTransition;
            return dummy;
        }

        protected Transform? _originalParent;
        protected Screen? _originalScreen;
        protected ViewController? _originalParentController;
        protected ViewController? _originalViewController;
        protected bool _originalIsInHierarchy;

        public bool deactivateAfterTransition;

        public void Init(ViewController originalViewController) {
            _originalViewController = originalViewController;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
            if (_originalViewController == null) throw new ArgumentNullException(nameof(_originalViewController));
            _originalScreen = _originalViewController.screen;
            _originalParentController = _originalViewController.parentViewController;
            _originalParent = transform.parent;
            _originalIsInHierarchy = _originalViewController.isInViewControllerHierarchy;
            _originalViewController!.__Init(screen, parentViewController, null);
            _originalViewController.__Activate(!_originalIsInHierarchy && addedToHierarchy, screenSystemEnabling);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling) {
            if (deactivateAfterTransition) {
                _originalViewController!.__Deactivate(removedFromHierarchy, false, screenSystemDisabling);
            }
            _originalViewController.SetField("_screen", _originalScreen);
            _originalViewController.SetField("_parentViewController", _originalParentController);
            _originalViewController!.__Deactivate(!_originalIsInHierarchy && removedFromHierarchy, false, screenSystemDisabling);
            transform.SetParent(_originalParent);
        }
    }
}