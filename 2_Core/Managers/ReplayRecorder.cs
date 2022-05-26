using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatLeader.Core.Managers.NoteEnhancer;
using BeatLeader.Core.Managers.ReplayEnhancer;
using BeatLeader.Models;
using BeatLeader.Utils;
using HarmonyLib;
using IPA.Loader;
using IPA.Utilities;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;
using Transform = BeatLeader.Models.Transform;

namespace BeatLeader {
    [UsedImplicitly]
    public class ReplayRecorder : IInitializable, IDisposable, ILateTickable {
        #region Constructor
        [Inject] [UsedImplicitly] private PlayerTransforms _playerTransforms;
        [Inject] [UsedImplicitly] private BeatmapObjectManager _beatmapObjectManager;
        [Inject] [UsedImplicitly] private BeatmapObjectSpawnController _beatSpawnController;
        [Inject] [UsedImplicitly] private StandardLevelScenesTransitionSetupDataSO _transitionSetup;
        [Inject] [UsedImplicitly] private PauseController _pauseController;
        [Inject] [UsedImplicitly] private AudioTimeSyncController _timeSyncController;
        [Inject] [UsedImplicitly] private ScoreController _scoreController;
        [Inject] [UsedImplicitly] private GameEnergyCounter _gameEnergyCounter;
        [Inject] [UsedImplicitly] private TrackingDeviceEnhancer _trackingDeviceEnhancer;

        private readonly PlayerHeightDetector _playerHeightDetector;
        private readonly Replay _replay = new();
        private Pause _currentPause;
        private WallEvent _currentWallEvent;
        private DateTime _pauseStartTime;
        private bool _stopRecording;

        public ReplayRecorder() {
            _playerHeightDetector = Resources.FindObjectsOfTypeAll<PlayerHeightDetector>().Last();

            UserEnhancer.Enhance(_replay); 

            PluginMetadata metaData = PluginManager.GetPluginFromId("BeatLeader");
            _replay.info.version = metaData.HVersion.ToString();
            _replay.info.gameVersion = Application.version;
            _replay.info.timestamp = Convert.ToString((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

            _stopRecording = false;
        }

        #endregion

        #region NoteIdHandling

        private readonly Dictionary<int, NoteEvent> _noteEventCache = new();
        private readonly Dictionary<int, SwingRatingCounterDidFinishReceiver> _finishReceiversCache = new();

        private readonly Dictionary<NoteController, int> _noteIdCache = new();
        private int _noteId;

        private readonly Dictionary<ObstacleController, int> _wallCache = new();
        private readonly Dictionary<int, WallEvent> _wallEventCache = new();
        private int _wallId;

        #endregion

        #region Events Subscription

        public void Initialize() {
            _beatmapObjectManager.noteWasSpawnedEvent += OnNoteWasSpawned;
            _beatmapObjectManager.obstacleWasSpawnedEvent += OnObstacleWasSpawned;
            _beatmapObjectManager.noteWasMissedEvent += OnNoteWasMissed;
            _beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
            _transitionSetup.didFinishEvent += OnTransitionSetupOnDidFinishEvent;
            _beatSpawnController.didInitEvent += OnBeatSpawnControllerDidInit;
            _scoreController.comboBreakingEventHappenedEvent += OnBreakCombo;
            _pauseController.didPauseEvent += OnPause;
            _pauseController.didResumeEvent += OnResume;
            if (_replay.info.height == 0)
            {
                _playerHeightDetector.playerHeightDidChangeEvent += OnPlayerHeightChange;
            }

            ScoreUtil.EnableSubmission();
        }

        public void Dispose() {
            _beatmapObjectManager.noteWasSpawnedEvent -= OnNoteWasSpawned;
            _beatmapObjectManager.obstacleWasSpawnedEvent -= OnObstacleWasSpawned;
            _beatmapObjectManager.noteWasMissedEvent -= OnNoteWasMissed;
            _beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
            _transitionSetup.didFinishEvent -= OnTransitionSetupOnDidFinishEvent;
            _beatSpawnController.didInitEvent -= OnBeatSpawnControllerDidInit;
            _scoreController.comboBreakingEventHappenedEvent -= OnBreakCombo;
            _pauseController.didPauseEvent -= OnPause;
            _pauseController.didResumeEvent -= OnResume;
            if (_replay.info.height == 0)
            {
                _playerHeightDetector.playerHeightDidChangeEvent -= OnPlayerHeightChange;
            }
        }

        #endregion

        #region Movements recording

        
        public void LateTick() {
            if (_timeSyncController == null || _playerTransforms == null || _currentPause != null || _stopRecording) return;

            var frame = new Frame() {
                time = _timeSyncController.songTime,
                fps = Mathf.RoundToInt(1.0f / Time.deltaTime),
                head = new Transform {
                    rotation = _playerTransforms.headPseudoLocalRot,
                    position = _playerTransforms.headPseudoLocalPos
                },
                leftHand = new Transform {
                    rotation = _playerTransforms.leftHandPseudoLocalRot,
                    position = _playerTransforms.leftHandPseudoLocalPos
                },
                rightHand = new Transform {
                    rotation = _playerTransforms.rightHandPseudoLocalRot,
                    position = _playerTransforms.rightHandPseudoLocalPos
                }
            };

            _replay.frames.Add(frame);

            if (_currentWallEvent != null) {
                PlayerHeadAndObstacleInteraction phaoi = _scoreController.GetField<PlayerHeadAndObstacleInteraction, ScoreController>("_playerHeadAndObstacleInteraction");

                if (phaoi != null && phaoi.intersectingObstacles.Count == 0)
                {
                    _currentWallEvent.energy = _gameEnergyCounter.energy;
                    _currentWallEvent = null;
                }
            }
        }

        #endregion

        #region OnNoteWasSpawned

        private void OnNoteWasSpawned(NoteController noteController) {
            if (_stopRecording) return;
            
            var noteId = _noteId++;
            _noteIdCache[noteController] = noteId;

            var noteData = noteController.noteData;

            NoteEvent noteEvent = new();
            noteEvent.noteID = noteData.lineIndex * 1000 + (int)noteData.noteLineLayer * 100 + (int)noteData.colorType * 10 + (int)noteData.cutDirection;
            noteEvent.spawnTime = noteData.time;
            _noteEventCache[noteId] = noteEvent;
        }

        #endregion

        #region OnObstacleWasSpawned

        private void OnObstacleWasSpawned(ObstacleController obstacleController)
        {
            if (_stopRecording) return;
            
            var wallId = _wallId++;
            _wallCache[obstacleController] = wallId;

            var obstacleData = obstacleController.obstacleData;

            WallEvent wallEvent = new();
            wallEvent.wallID = obstacleData.lineIndex * 100 + (int)obstacleData.obstacleType * 10 + obstacleData.width;
            wallEvent.spawnTime = obstacleData.time;
            _wallEventCache[wallId] = wallEvent;
        }

        #endregion

        #region OnNoteWasMissed

        private void OnNoteWasMissed(NoteController noteController) {
            if (_stopRecording) return;
            
            var noteId = _noteIdCache[noteController];

            if (noteController.noteData.colorType != ColorType.None)
            {
                var noteEvent = _noteEventCache[noteId];
                noteEvent.eventTime = _timeSyncController.songTime;
                noteEvent.eventType = NoteEventType.miss;
                _replay.notes.Add(noteEvent);
            }
        }

        #endregion

        #region OnBreakCombo

        private void OnBreakCombo()
        {
            if (_stopRecording) return;
            
            PlayerHeadAndObstacleInteraction phaoi = _scoreController.GetField<PlayerHeadAndObstacleInteraction, ScoreController>("_playerHeadAndObstacleInteraction");

            if (phaoi != null && phaoi.intersectingObstacles.Count > 0)
            {
                WallEvent wallEvent = _wallEventCache[_wallCache[phaoi.intersectingObstacles[0]]];
                wallEvent.time = _timeSyncController.songTime;
                _replay.walls.Add(wallEvent);
                _currentWallEvent = wallEvent;
            }
        }

        #endregion

        #region OnNoteWasCut

        private void OnNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo) {
            if (_stopRecording) return;
            
            var noteId = _noteIdCache[noteController];

            var noteEvent = _noteEventCache[noteId];
            noteEvent.eventTime = _timeSyncController.songTime;
            noteEvent.noteCutInfo = CreateNoteCutInfo(noteCutInfo);
            
            _replay.notes.Add(noteEvent);

            if (noteCutInfo.allIsOK) {
                noteEvent.eventType = NoteEventType.good;
            } else {
                var isBomb = noteController.noteData.colorType == ColorType.None;
                noteEvent.eventType = isBomb ? NoteEventType.bomb : NoteEventType.bad;
            }
            
            if (noteCutInfo.swingRatingCounter == null) return;
            var counterDidFinishReceiver = new SwingRatingCounterDidFinishReceiver(noteId, OnSwingRatingCounterDidFinish);
            noteCutInfo.swingRatingCounter.RegisterDidFinishReceiver(counterDidFinishReceiver);
            _finishReceiversCache[noteId] = counterDidFinishReceiver;
        }

        #endregion

        #region OnSwingRatingCounterDidFinish

        private void OnSwingRatingCounterDidFinish(ISaberSwingRatingCounter swingRatingCounter, int noteId) {
            if (_stopRecording) return;
            
            swingRatingCounter.UnregisterDidFinishReceiver(_finishReceiversCache[noteId]);
            var cutEvent = _noteEventCache[noteId];
            var noteCutInfo = cutEvent.noteCutInfo;
            var counter = (SaberSwingRatingCounter) swingRatingCounter;
            SwingRatingEnhancer.Enhance(noteCutInfo, counter);
            SwingRatingEnhancer.Reset(counter);
        }

        #endregion

        #region SwingRatingCounterDidFinishReceiver

        private class SwingRatingCounterDidFinishReceiver : ISaberSwingRatingCounterDidFinishReceiver {
            private readonly Action<ISaberSwingRatingCounter, int> _finishEvent;
            private readonly int _noteId;

            public SwingRatingCounterDidFinishReceiver(int noteId, Action<ISaberSwingRatingCounter, int> finishEvent) {
                _finishEvent = finishEvent;
                _noteId = noteId;
            }

            public void HandleSaberSwingRatingCounterDidFinish(ISaberSwingRatingCounter saberSwingRatingCounter) {
                _finishEvent.Invoke(saberSwingRatingCounter, _noteId);
            }
        }

        #endregion

        #region JD
        private void OnBeatSpawnControllerDidInit()
        {
            _replay.info.jumpDistance = _beatSpawnController.jumpDistance;
        }
        #endregion

        #region Map finish
        private void OnTransitionSetupOnDidFinishEvent(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results)
        {
            _stopRecording = true;
            _replay.notes.RemoveAll(note => note.eventType == NoteEventType.unknown);
            
            _replay.info.score = _scoreController.prevFrameRawScore;
            MapEnhancer.energy = results.energy; 
            MapEnhancer.Enhance(_replay);
            _trackingDeviceEnhancer.Enhance(_replay);
            switch (results.levelEndStateType)
            {
                case LevelCompletionResults.LevelEndStateType.Cleared:
                    Plugin.Log.Info("Level Cleared. Save replay");
                    ScoreUtil.instance.ProcessReplayAsync(_replay);
                    break;
                case LevelCompletionResults.LevelEndStateType.Failed:
                    if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Restart)
                    {
                        Plugin.Log.Info("Restart");
                    }
                    else
                    {
                        _replay.info.failTime = _timeSyncController.songTime;
                        Plugin.Log.Info("Level Failed. Save replay");
                        ScoreUtil.instance.ProcessReplayAsync(_replay);
                    }
                    break;
            }

            switch (results.levelEndAction)
            {
                case LevelCompletionResults.LevelEndAction.Quit:
                    
                    break;
                case LevelCompletionResults.LevelEndAction.Restart:
                    
                    break;
            }
        }
        #endregion

        #region Height
        private void OnPlayerHeightChange(float height)
        {
            AutomaticHeight automaticHeight = new();
            automaticHeight.height = height;
            automaticHeight.time = _timeSyncController.songTime;

            _replay.heights.Add(automaticHeight);

        }
        #endregion

        #region Pause
        private void OnPause()
        {
            _currentPause = new();
            _currentPause.time = _timeSyncController.songTime;
            _pauseStartTime = DateTime.Now;
        }

        private void OnResume()
        {
            _currentPause.duration = DateTime.Now.ToUnixTime() - _pauseStartTime.ToUnixTime();
            _replay.pauses.Add(_currentPause);
            _currentPause = null;
        }
        #endregion

        #region Utils
        
        private static Models.NoteCutInfo CreateNoteCutInfo(NoteCutInfo cutInfo) {
            return new Models.NoteCutInfo {
                speedOK = cutInfo.speedOK,
                directionOK = cutInfo.directionOK,
                saberTypeOK = cutInfo.saberTypeOK,
                wasCutTooSoon = cutInfo.wasCutTooSoon,
                saberSpeed = cutInfo.saberSpeed,
                saberDir = cutInfo.saberDir,
                saberType = (int) cutInfo.saberType,
                timeDeviation = cutInfo.timeDeviation,
                cutDirDeviation = cutInfo.cutDirDeviation,
                cutPoint = cutInfo.cutPoint,
                cutNormal = cutInfo.cutNormal,
                cutDistanceToCenter = cutInfo.cutDistanceToCenter,
                cutAngle = cutInfo.cutAngle
            };
        }
        
        #endregion
    }
}