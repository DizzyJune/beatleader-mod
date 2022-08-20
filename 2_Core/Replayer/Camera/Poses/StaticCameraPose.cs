﻿using static BeatLeader.Replayer.Managers.InputManager;
using BeatLeader.Models;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using Pose = UnityEngine.Pose;

namespace BeatLeader.Replayer.Poses
{
    public struct StaticCameraPose : ICameraPoseProvider
    { 
        public StaticCameraPose(string name, Vector3 position, Quaternion rotation)
        {
            _rotation = rotation;
            _position = position;
            _name = name;
            _availableInputs = new[] { InputType.FPFC, InputType.VR };
        }
        public StaticCameraPose(string name, Vector3 position, Quaternion rotation, params InputType[] availableInputs)
        {
            _rotation = rotation;
            _position = position;
            _name = name;
            _availableInputs = availableInputs;
        }

        private InputType[] _availableInputs;
        private Quaternion _rotation;
        private Vector3 _position;
        private string _name;

        public InputType[] AvailableInputs => _availableInputs;
        public bool SupportsOffset => false;
        public bool UpdateEveryFrame => false;
        public string Name => _name;

        public Pose GetPose(Pose cameraPose) => new Pose(_position, _rotation);
    }
}
