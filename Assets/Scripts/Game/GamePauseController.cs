using UnityEngine;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Core;
using AttackSkill.UI;

namespace AttackSkill.Game
{
    /// <summary>
    /// GameScene：ESC 开关独立暂停菜单；设置从暂停菜单进入，关设置仍保持暂停。
    /// </summary>
    public class GamePauseController : MonoBehaviour
    {
        [SerializeField] KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] bool pauseAudio = true;
        [SerializeField] bool relockCursorOnResume = true;
        [SerializeField] ThirdPersonCamera thirdPersonCamera;
        [SerializeField] bool autoFindCamera = true;

        bool _pauseSession;

        void Awake()
        {
            if (autoFindCamera && thirdPersonCamera == null)
            {
                thirdPersonCamera = GameServices.ResolveCamera();
            }
        }

        void OnEnable()
        {
            GamePause.PauseChanged += OnPauseChanged;
        }

        void OnDisable()
        {
            GamePause.PauseChanged -= OnPauseChanged;
            EndPauseSession(closeUi: true);
            GamePause.ForceResume();
            if (pauseAudio)
            {
                AudioListener.pause = false;
            }
        }

        void Update()
        {
            if (!GameInput.GetKeyDown(pauseKey))
            {
                SyncIfUiClosedExternally();
                return;
            }

            HandleEscape();
        }

        void HandleEscape()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogWarning("[GamePause] 无 UIManager（需从 OpenScene 带入 UIRoot）。", this);
                return;
            }

            // 全灭结算期间禁止 ESC 开暂停
            if (ui.IsOpen(UIId.GameOver))
            {
                return;
            }

            // 技能轮盘优先关闭（不进入暂停菜单）
            if (ui.IsOpen(UIId.SkillWheel))
            {
                ui.Close(UIId.SkillWheel);
                return;
            }

            // 设置叠在暂停上：ESC 只关设置，仍停在暂停菜单
            if (ui.IsOpen(UIId.Setting))
            {
                ui.Close(UIId.Setting);
                if (_pauseSession && !ui.IsOpen(UIId.PauseMenu))
                {
                    OpenPauseMenu(ui);
                }

                return;
            }

            if (_pauseSession || ui.IsOpen(UIId.PauseMenu))
            {
                EndPauseSession(closeUi: true);
                return;
            }

            BeginPauseSession(ui);
        }

        void SyncIfUiClosedExternally()
        {
            if (!_pauseSession)
            {
                return;
            }

            var ui = UIManager.Instance;
            if (ui == null)
            {
                return;
            }

            // 暂停菜单与设置都被关掉 → 结束暂停
            if (!ui.IsOpen(UIId.PauseMenu) && !ui.IsOpen(UIId.Setting))
            {
                EndPauseSession(closeUi: false);
            }
        }

        void BeginPauseSession(UIManager ui)
        {
            _pauseSession = true;
            GamePause.SetPaused(true);
            OpenPauseMenu(ui);
        }

        void OpenPauseMenu(UIManager ui)
        {
            ui.EnsurePauseMenuRegistered();
            ui.OpenDialog(UIId.PauseMenu, new UIPauseMenuDialogArgs
            {
                onContinue = () => EndPauseSession(closeUi: true),
                onOpenSettings = () =>
                {
                    if (!_pauseSession)
                    {
                        _pauseSession = true;
                        GamePause.SetPaused(true);
                    }

                    ui.OpenDialog(UIId.Setting);
                },
                onReset = () =>
                {
                    EndPauseSession(closeUi: true);
                    PartyController.Instance?.ResetToBeachRun();
                },
                onQuit = QuitGame
            });
        }

        void EndPauseSession(bool closeUi)
        {
            _pauseSession = false;

            if (closeUi)
            {
                var ui = UIManager.Instance;
                if (ui != null)
                {
                    if (ui.IsOpen(UIId.Setting))
                    {
                        ui.Close(UIId.Setting);
                    }

                    if (ui.IsOpen(UIId.PauseMenu))
                    {
                        ui.Close(UIId.PauseMenu);
                    }
                }
            }

            GamePause.SetPaused(false);
        }

        void OnPauseChanged(bool paused)
        {
            if (pauseAudio)
            {
                AudioListener.pause = paused;
            }

            if (paused)
            {
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.SetCursorLocked(false);
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else if (relockCursorOnResume && !GameplayInputGate.IsSoftBlocked)
            {
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.RestoreDesiredCursorLock();
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public static void QuitGame()
        {
            GameProgressController.Instance?.TrySave("Quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
