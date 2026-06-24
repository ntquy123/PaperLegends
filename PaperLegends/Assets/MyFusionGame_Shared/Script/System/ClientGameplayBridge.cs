using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central bridge that safely exposes client-only systems to shared/server code.
/// </summary>
public static class ClientGameplayBridge
{
    public static class PlayerMovement
    {
        public static bool HasInstance()
        {
#if !UNITY_SERVER
            return MovePlayerOnlineHandler.Instance != null;
#else
            return false;
#endif
        }

        public static void StopMovementLoop()
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.StopMovementLoop();
#endif
        }

        public static bool TryGetInput(out PlayerInputData inputData)
        {
#if !UNITY_SERVER
            if (MovePlayerOnlineHandler.Instance != null && MovePlayerOnlineHandler.Instance.CanSendNetworkInput())
            {
                inputData = MovePlayerOnlineHandler.Instance.GetInput();
                return true;
            }
#endif
            inputData = default;
            return false;
        }

        public static void StartMoveLeft()
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.StartMoveLeft();
#endif
        }

        public static void StopMoveLeft()
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.StopMoveLeft();
#endif
        }

        public static void StartMoveRight()
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.StartMoveRight();
#endif
        }

        public static void StopMoveRight()
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.StopMoveRight();
#endif
        }

        public static void SetYaw(float yaw)
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.SetYaw(yaw);
#endif
        }

//        public static void QueueShotInput(ShotParams shot)
//        {
//#if !UNITY_SERVER
//            MovePlayerOnlineHandler.Instance?.QueueShotInput(shot);
//#endif
//        }

        public static void RequestAnimState(CharacterAnimState state)
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.RequestAnimState(state);
#endif
        }
        public static void RotateSightingPoint(Vector3 lookAtTarget)
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.RotateSightingPoint(lookAtTarget);
#endif
        }

        public static void ApplyServerRotation(float yaw, float pitch)
        {
#if !UNITY_SERVER
            MovePlayerOnlineHandler.Instance?.ApplyServerRotation(yaw, pitch);
#endif
        }
    }

    public static class Loading
    {
        public static void EnsureSceneActive(string sceneName)
        {
#if !UNITY_SERVER
            LoadingManager.Instance?.EnsureSceneActive(sceneName);
#endif
        }

        public static void FinishGameLoading()
        {
#if !UNITY_SERVER
            var loadingManager = LoadingManager.Instance;
            if (loadingManager == null)
                return;

            loadingManager.FinishLoading();
            loadingManager.HideReconnectLoading();

            if (loadingManager.UILoadingScreenPrefab != null)
                loadingManager.UILoadingScreenPrefab.SetActive(false);
#endif
        }
    }

    public static class Sound
    {
        public enum Channel
        {
            Bgm,
            Sfx,
            Ui,
            Voice,
            Heartbeat
        }

        public static bool HasInstance()
        {
#if !UNITY_SERVER
            return SoundManager.Instance != null;
#else
            return false;
#endif
        }

        public static void PlayBackground()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBackGroundSound();
#endif
        }

        public static void PlayYourTurn()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayYourTurn();
#endif
        }

        public static void PlayOpponentTurn()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayOpponentTurn();
#endif
        }

        public static void PlayExamTurn()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayExamTurn();
#endif
        }

        public static void PlayBallHit(float force, Vector3 position)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHit(force, position);
#endif
        }

        public static void PlayBallHitWater(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitWater(position, force);
#endif
        }

        public static void PlayBallHitPuddle(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitPuddle(position, force);
#endif
        }

        public static void PlayBallHitGrass(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitGrass(position, force);
#endif
        }

        public static void PlayBallHitSwamp(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitSwamp(position, force);
#endif
        }

        public static void PlayBallHitRock(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitRock(position, force);
#endif
        }

        public static void PlayBallHitTree(Vector3 position, float force = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallHitTree(position, force);
#endif
        }

        public static void PlayBallPlayerHitKeng()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBallPlayerHitKeng();
#endif
        }

        public static void PlayFloodWarning()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayFloodWarning();
#endif
        }

        public static void PlayStepInWater(Vector3 position)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayStepInWater(position);
#endif
        }

        public static void PlayShoot()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayShootAudio();
#endif
        }

        public static void PlayShotBallGroundImpact(Vector3 position, float intensity = 1f)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayShotBallGroundImpact(position, intensity);
#endif
        }

        public static void StartShotBallRollingLoop(GameObject owner, Func<float> getSpeed)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StartShotBallRollingLoop(owner, getSpeed);
#endif
        }

        public static void StopShotBallRollingLoop(GameObject owner)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StopShotBallRollingLoop(owner);
#endif
        }

        public static void PlayKillCamShooterPredicted()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayKillCamShooterPredicted();
#endif
        }

        public static void PlayKillCamShooterHit()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayKillCamShooterHit();
#endif
        }

        public static void PlayKillCamVictimHit()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayKillCamVictimHit();
#endif
        }

        public static void PlayBeep()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayBeepSound();
#endif
        }

        public static void PlayAngryPower()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayAngryPower();
#endif
        }

        public static void PlayCombo(int combo)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayComboAudio(combo);
#endif
        }

        public static void PlayPlayerEliminated()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayPlayerEliminated();
#endif
        }

        public static void PlayButton(ButtonSfx type)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayButtonSfx(type);
#endif
        }

        public static void StartFootstepLoop(GameObject owner)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StartFootstepLoop(owner);
#endif
        }

        public static void StopFootstepLoop()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StopFootstepLoop();
#endif
        }

        public static void StopFootstepLoop(GameObject owner)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StopFootstepLoop(owner);
#endif
        }

        public static void StopHeartbeatLoop()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StopHeartbeatLoop();
#endif
        }

        public static void StartHeartbeatLoop()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayHeartbeatLoop();
#endif
        }

        public static void StopBallRollingLoop(GameObject owner)
        {
#if !UNITY_SERVER
            SoundManager.Instance?.StopBallRollingLoop(owner);
#endif
        }

        public static void PlayUpgradeExplosion()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayUpgradeExplosion();
#endif
        }

        public static void PlayUpgradeSuccess()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayUpgradeSuccess();
#endif
        }

        public static void PlayUpgradeFailure()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayUpgradeFailure();
#endif
        }

        public static void PlayFusionExplosion()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayFusionExplosion();
#endif
        }

        public static void PlayFusionSuccess()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayFusionSuccess();
#endif
        }

        public static void PlayFusionFailure()
        {
#if !UNITY_SERVER
            SoundManager.Instance?.PlayFusionFailure();
#endif
        }

        public static void PlayCustomSfx(AudioClip clip)
        {
#if !UNITY_SERVER
            if (clip == null)
                return;
            var manager = SoundManager.Instance;
            manager?.sfxSource?.PlayOneShot(clip);
#endif
        }

        public static AudioSource GetChannelSource(Channel channel)
        {
#if !UNITY_SERVER
            var manager = SoundManager.Instance;
            if (manager == null)
                return null;
            switch (channel)
            {
                case Channel.Bgm:
                    return manager.bgmSource;
                case Channel.Sfx:
                    return manager.sfxSource;
                case Channel.Ui:
                    return manager.uiSource;
                case Channel.Voice:
                    return manager.voiceSource;
                case Channel.Heartbeat:
                    return manager.heartbeatSource;
            }
#endif
            return null;
        }

        public static void SetChannelVolume(Channel channel, float volume)
        {
#if !UNITY_SERVER
            var source = GetChannelSource(channel);
            if (source != null)
                source.volume = volume;
#endif
        }
    }

    public static class Skill
    {
        private const int BallGrazeHitSkillId = 11400004;

        public static bool IsGrazeHitSkillId(int skillId)
        {
#if !UNITY_SERVER
            return SkillManager.IsGrazeHitSkillId(skillId);
#else
            return skillId == (int)EffectPlayerType.GrazeHit ||
                   skillId == (int)EffectPlayerType.HeavyBallSkill ||
                   skillId == BallGrazeHitSkillId;
#endif
        }

        public static float GetChamCatRadius(float fallback)
        {
#if !UNITY_SERVER
            if (SkillManager.Instance != null && SkillManager.Instance.DistanceForChamCat > 0f)
                return SkillManager.Instance.DistanceForChamCat;
#endif
            return fallback;
        }

        public static void ClearSkillUsageHistory()
        {
#if !UNITY_SERVER
            SkillManager.Instance?.ClearSkillUsageHistory();
#endif
        }

        public static void OnSkillUsedByPlayer(int playerId, int skillId, bool isCountered = false)
        {
#if !UNITY_SERVER
            SkillManager.Instance?.OnSkillUsedByPlayer(playerId, skillId, isCountered);
#endif
        }

        public static bool ShouldShowChamCatIconForTarget(int playerId, BallServerController target)
        {
#if !UNITY_SERVER
            return SkillManager.Instance != null &&
                   SkillManager.Instance.ShouldShowChamCatIconForTarget(playerId, target);
#else
            return false;
#endif
        }

        public static void ShowSkillList()
        {
#if !UNITY_SERVER
            SkillManager.Instance?.ShowSkilldList();
#endif
        }
    }

    public static class Vfx
    {
#if !UNITY_SERVER
        private const float WaterSplashDedupSeconds = 0.35f;
        private const float WaterSplashDedupDistanceSqr = 0.64f;
        private static readonly Dictionary<int, float> LastWaterSplashTimeByPlayer = new Dictionary<int, float>();
        private static readonly Dictionary<int, Vector3> LastWaterSplashPositionByPlayer = new Dictionary<int, Vector3>();
#endif

        public static void PlayWaterSplash(Vector3 position, int playerId = 0)
        {
#if !UNITY_SERVER
            if (ShouldSuppressWaterSplash(position, playerId))
                return;

            var waterSplashVfxPrefab = GameInitializer.Instance != null
                ? GameInitializer.Instance.WaterSplashVfxPrefab
                : null;
            if (waterSplashVfxPrefab == null)
            {
                Debug.LogWarning("[ClientGameplayBridge] WaterSplashVfxPrefab is not assigned in GameInitializer.");
                return;
            }

            var vfxInstance = UnityEngine.Object.Instantiate(waterSplashVfxPrefab, position, Quaternion.identity);
            var particleSystem = vfxInstance.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var lifetime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
                UnityEngine.Object.Destroy(vfxInstance, Mathf.Max(lifetime, 0.1f));
            }
            else
            {
                UnityEngine.Object.Destroy(vfxInstance, 2f);
            }
#endif
        }

#if !UNITY_SERVER
        private static bool ShouldSuppressWaterSplash(Vector3 position, int playerId)
        {
            int key = playerId > 0 ? playerId : 0;
            float now = Time.unscaledTime;

            if (LastWaterSplashTimeByPlayer.TryGetValue(key, out var lastTime) &&
                LastWaterSplashPositionByPlayer.TryGetValue(key, out var lastPosition) &&
                now - lastTime <= WaterSplashDedupSeconds &&
                (lastPosition - position).sqrMagnitude <= WaterSplashDedupDistanceSqr)
            {
                return true;
            }

            LastWaterSplashTimeByPlayer[key] = now;
            LastWaterSplashPositionByPlayer[key] = position;
            return false;
        }
#endif
    }

    public static class Gameplay
    {
#if !UNITY_SERVER
        private static BoxCollider playArea;
#endif

        public static void SetPlayArea(BoxCollider area)
        {
#if !UNITY_SERVER
            playArea = area;
#endif
        }

        public static void ClearPlayArea(BoxCollider area)
        {
#if !UNITY_SERVER
            if (playArea == area)
            {
                playArea = null;
            }
#endif
        }

        public static Transform GetPlayAreaTransform()
        {
#if !UNITY_SERVER
            return playArea != null ? playArea.transform : null;
#else
            return null;
#endif
        }
    }

//    public static class GameSession
//    {
//        public static void ConfirmPendingShot()
//        {
//#if !UNITY_SERVER
//            GameSessionClientLocal.Instance?.ConfirmPendingShot();
//#endif
//        }
//    }

    public static class Camera
    {
        public static bool HasInstance()
        {
#if !UNITY_SERVER
            return CameraRotation.Instance != null;
#else
            return true;
#endif
        }

        public static void StopSlowMotion()
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StopSlowMotion();
#endif
        }

        public static void StartFollowingPlayerOnline(Transform target)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingPlayerOnline(target);
#endif
        }

        public static void StartFollowingPaperLegendCharacter(Transform target)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingPaperLegendCharacter(target);
#endif
        }

        public static void StartFollowingBallForMyself(int playerId, int isExam)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingBallForMyself(playerId, isExam);
#endif
        }

        public static void StartFollowingBallOtherPlayer(int playerId, int isExam)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingBallOtherPlayer(playerId, isExam);
#endif
        }

        public static void PlaySlowMotionWithDetection(Vector3 direction)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.PlaySlowMotionWithDetection(direction);
#endif
        }

        public static void PlayKillCamSlowMotionShooter(int shooterId, Transform shooterTarget, Transform focusTarget, Vector3 predictedPoint, float predictedTimeToHit)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.PlayKillCamSlowMotionShooter(shooterId, shooterTarget, focusTarget, predictedPoint, predictedTimeToHit);
#endif
        }

        public static void PlayKillCamSlowMotionVictim(int shooterId, Transform shooterTarget, Transform victimTarget, Vector3 predictedPoint, float predictedTimeToHit)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.PlayKillCamSlowMotionVictim(shooterId, shooterTarget, victimTarget, predictedPoint, predictedTimeToHit);
#endif
        }

        public static void ConfirmKillCamHit(int shooterId, int victimId, Transform shooterTarget, Transform victimTarget, Vector3 hitPoint)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.ConfirmKillCamHit(shooterId, victimId, shooterTarget, victimTarget, hitPoint);
#endif
        }

        public static void EndKillCamSlowMotion(int shooterId)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.EndKillCamSlowMotion(shooterId);
#endif
        }

        public static void SetMiniCameraActive(bool active)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.SetMiniCameraActive(active);
#endif
        }

        public static void PlayFloodCinematic(Transform waterTarget)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.PlayFloodCinematic(waterTarget);
#endif
        }

//        public static void MoveCameraToFppOnline(Transform target, Transform lookAt)
//        {
//#if !UNITY_SERVER
//            CameraRotation.Instance?.MoveCameraToFPPOnline(target, lookAt);
//#endif
//        }

//        public static void MoveCameraToFpp(Vector3 position, Vector3 lookAt)
//        {
//#if !UNITY_SERVER
//            CameraRotation.Instance?.MoveCameraToFPP(position, lookAt);
//#endif
//        }

        public static void MoveCameraViewMap()
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.MoveCameraViewMap();
#endif
        }

        public static void RotateCameraToPoint(Vector3 point)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.RotateCameraToPoint(point);
#endif
        }

        public static void StartFollowingAI(Transform target)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingAI(target);
#endif
        }

        public static void StartFollowingBall(Transform target)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StartFollowingBall(target);
#endif
        }

        public static void OnBallShot(Transform ballTransform, int playerId, bool hasStateAuthority, int isExam)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.HandleBallShot(ballTransform, playerId, hasStateAuthority, isExam);
#endif
        }

        public static void OnBallShotReset(int playerId)
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.ResetBallShot(playerId);
#endif
        }

        public static void StopCameraLoop()
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StopCameraLoop();
#endif
        }

        public static void StopFollowingBall()
        {
#if !UNITY_SERVER
            CameraRotation.Instance?.StopFollowingBall();
#endif
        }
    }

    public static class Match
    {
        public static void ShowGameOverResults(List<OverGameRequest> results)
        {
#if !UNITY_SERVER
            Camera.StopSlowMotion();
            Camera.SetMiniCameraActive(false);

            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }

            GameOverManager.Instance.ShowGameOverResults(results);
#endif
        }
    }

    public static class UI
    {
        public static bool HasInstance()
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance != null;
#else
            return false;
#endif
        }

        public static void ShowPlayerList()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowPlayerList_Online();
#endif
        }

        public static void ShowInfoList()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowInforList_Online();
#endif
        }

        public static void UpdateViewMapButtonState()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UpdateViewMapButtonState();
#endif
        }

        public static void UpdateHidePlayerButtonState()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UpdateHidePlayerButtonState();
#endif
        }

        public static void ResetHiddenPlayers()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ResetHiddenPlayers();
#endif
        }

        public static void UpdateTurnCounter(int round, int maxRound)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UpdateTurnCounter(round, maxRound);
#endif
        }

        public static void ShowTurnIndicator(string message, float duration, float delay)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowTurnIndicator(message, duration, delay);
#endif
        }

        public static IEnumerator ShowTurnIndicatorRunTime(string message, float duration, float delay)
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance?.ShowTurnIndicatorRunTime(message, duration, delay);
#else
            return null;
#endif
        }

        public static void ShowImpactAnnouncement(string title, string localizationKey, float displayDuration = 1.4f)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowImpactAnnouncement(title, localizationKey, displayDuration);
#endif
        }

        public static IEnumerator ShowImpactAnnouncementRunTime(string title, string localizationKey, float displayDuration = 1.4f)
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance?.ShowImpactAnnouncementRunTime(title, localizationKey, displayDuration);
#else
            return null;
#endif
        }

        public static void ShowMessage(string message, float width = 300f, float displayTime = 2f)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowMessage(message, width, displayTime);
#endif
        }

        public static void ShowMessageByUser(string message)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowMesByUser(message);
#endif
        }

        public static void ShowDestroyPermissionUnlockedAnnouncement()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.ShowDestroyPermissionUnlockedAnnouncement();
#endif
        }


        public static void ShowStartPointUI()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UIforStartPointOnline();
#endif
        }

        public static void ShowPlayNormalUI()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UIforPlayNormalOnline();
#endif
        }

        public static void ShowViewUI()
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.UIforViewOnline();
#endif
        }

        public static Transform GetCanvasTransform()
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance?.canvasTransform;
#else
            return null;
#endif
        }

        public static bool HasSelectedSkills()
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance != null && UIControllerOnline.Instance.selectedSkills != null;
#else
            return false;
#endif
        }

        public static System.Collections.Generic.List<SkillType> GetSelectedSkills()
        {
#if !UNITY_SERVER
            return UIControllerOnline.Instance?.selectedSkills;
#else
            return null;
#endif
        }

        public static void StopTurnCountdown(bool playerDidAction)
        {
#if !UNITY_SERVER
            UIControllerOnline.Instance?.StopTurnCountdown(playerDidAction);
#endif
        }
    }

    public static class Popup
    {
        public static bool HasInstance()
        {
#if !UNITY_SERVER
            return PopupHelper.Instance != null;
#else
            return false;
#endif
        }

        public static void ShowPopupConfirm(string message)
        {
#if !UNITY_SERVER
            PopupHelper.Instance?.ShowPopupConfirm(message);
#else
            Debug.Log($"[Popup] {message}");
#endif
        }

        public static void ShowRewardReveal(RewardClaimResponse modelData)
        {
#if !UNITY_SERVER
            PopupHelper.Instance?.ShowRewardReveal(modelData);
#else
            Debug.Log("[Popup] ShowRewardReveal invoked on server build.");
#endif
        }
    }
    public static class ChatBridge
    {
        public static void ShowChat(string senderName, string message)
        {
#if !UNITY_SERVER
           ChatController.Instance.ShowChat(senderName, message);
#endif
        }

        public static void ShowSystemMessage(string message)
        {
#if !UNITY_SERVER
            ChatController.Instance.ShowSystemMessage(message);
#endif
        }
    }
}
