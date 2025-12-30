/*
MIT License

Copyright (c) 2023 Èric Canela
Contact: knela96@gmail.com or @knela96 twitter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (Dynamic Parkour System), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;


/* OPIS
 =====================================================================
 THIRD PERSON CONTROLLER (DPS – Dynamic Parkour System)
 =====================================================================

 ROLA W SYSTEMIE:
 ----------------
 ThirdPersonController jest CENTRALNYM koordynatorem logiki postaci.
 Nie wykonuje fizyki ani IK samodzielnie – deleguje to do:
   - MovementCharacterController (Rigidbody, prędkość, grawitacja)
   - AnimationCharacterController (animacje, IK rąk / stóp)
   - DetectionCharacterController (raycasty: podłoże, krawędzie)
   - VaultingController (parkour: vault, slide, climb)

 Ten skrypt:
   ✔ zbiera input
   ✔ przelicza go na kierunki świata
   ✔ ustawia prędkość ruchu
   ✔ obraca postać względem kamery
   ✔ utrzymuje flagi stanu (isGrounded, isJumping, isVaulting itd.)

 WAŻNE:
 ------
 - NIE steruje Rigidbody bezpośrednio
 - NIE wyłącza IK
 - NIE zmienia trybu fizyki
 - Jest bezpiecznym miejscem do:
     • zarządzania trybami poruszania (TPP / FPP / Parkour / Climb)
     • mapowania inputu
     • decydowania „co wolno” w danym trybie

 =====================================================================
*/

/*

namespace Climbing
{
    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(AnimationCharacterController))]
    [RequireComponent(typeof(DetectionCharacterController))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(VaultingController))]
    public class ThirdPersonController : MonoBehaviour
    {
        // =================================================================
        // REFERENCES DO PODSYSTEMÓW DPS
        // =================================================================

        [HideInInspector] public InputCharacterController characterInput;
        [HideInInspector] public MovementCharacterController characterMovement;
        [HideInInspector] public AnimationCharacterController characterAnimation;
        [HideInInspector] public DetectionCharacterController characterDetection;
        [HideInInspector] public VaultingController vaultingController;

        // =================================================================
        // FLAGI STANU POSTACI (UŻYWANE PRZEZ INNE SYSTEMY)
        // =================================================================

        [HideInInspector] public bool isGrounded = false; // Czy stoimy na podłożu
        [HideInInspector] public bool allowMovement = true; // Globalna zgoda na ruch
        [HideInInspector] public bool onAir = false; // Czy postać jest w powietrzu
        [HideInInspector] public bool isJumping = false; // Aktywny stan skoku
        [HideInInspector] public bool inSlope = false; // Czy poruszamy się po pochyłości
        [HideInInspector] public bool isVaulting = false; // Akcja parkour trwa
        [HideInInspector] public bool dummy = false; // DPS używa tego do blokad animacji

        // =================================================================
        // KAMERY (TPP – klasyczne DPS)
        // =================================================================

        [Header("Climbing")]
        [SerializeField] private Climbing.ClimbController climbController;

        [Header("Cameras")]
        public CameraController cameraController;
        public Transform mainCamera;   // Kamera główna (referencja rotacji)
        public Transform freeCamera;   // Kamera pomocnicza do wyliczania kierunku ruchu

        [Header("Camera / Zoom Systems")]
        [SerializeField] private FreeLookZoomInputSystem freeLookZoomInput;
        [SerializeField] private SwitchCameras switchCameras;
        [SerializeField] private GameObject characterVisualRoot;

        // =================================================================
        // USTAWIENIA FPP
        // =================================================================

        [Header("FPP Settings")]
        [SerializeField] private Transform playModel;   // root postaci / model
        [SerializeField] private float fppMouseSensitivity = 60f;

        // =================================================================
        // USTAWIENIA AUTO-STEP (używane w MovementCharacterController)
        // =================================================================

        [Header("Step Settings")]
        [Range(0, 10.0f)] public float stepHeight = 0.8f;
        public float stepVelocity = 0.2f;

        // =================================================================
        // KOLIDERY (normalny / ślizg)
        // =================================================================

        [Header("Colliders")]
        public CapsuleCollider normalCapsuleCollider;
        public CapsuleCollider slidingCapsuleCollider;

        // =================================================================
        // OBRÓT POSTACI
        // =================================================================

        private float turnSmoothTime = 0.1f;
        private float turnSmoothVelocity;

        // =================================================================
        // TRYBY PORUSZANIA – SZKIELET (NIE ZMIENIA JESZCZE LOGIKI)
        // =================================================================

        public enum LocomotionMode
        {
            TPP,        // klasyczne FreeLook (domyślny DPS)
            FPP,        // First Person
            Parkour,    // Vault / Slide / Wall actions (TPP)
            Climbing,   // Wspinaczka (oddzielna kamera)
            Disable     // Pauza / blokada logiczna (na razie pusta)
        }

        [HideInInspector] public LocomotionMode currentMode = LocomotionMode.TPP;
        LocomotionMode previousMode;


        // =================================================================
        // UNITY LIFECYCLE
        // =================================================================

        private void Awake()
        {
            // Pobranie referencji do wszystkich podsystemów DPS
            characterInput = GetComponent<InputCharacterController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterAnimation = GetComponent<AnimationCharacterController>();
            characterDetection = GetComponent<DetectionCharacterController>();
            vaultingController = GetComponent<VaultingController>();

            if (cameraController == null)
                Debug.LogError("Attach the Camera Controller located in the Free Look Camera");
        }

        private void Start()
        {
            characterMovement.OnLanded += characterAnimation.Land;
            characterMovement.OnFall += characterAnimation.Fall;

            if (freeLookZoomInput != null)
            {
                freeLookZoomInput.OnRequestEnterFPP += TryEnterFPP;
                freeLookZoomInput.OnRequestExitFPP += TryExitFPP;
            }
        }

        // =================================================================
        // UPDATE – GŁÓWNA PĘTLA LOGIKI POSTACI
        // =================================================================


        void Update()
        {
            isGrounded = OnGround();

            // -------------------------------------------------
            // WEJŚCIE DO CLIMBING (JEDNORAZOWE)
            // -------------------------------------------------
            if (currentMode != LocomotionMode.Climbing &&
                climbController != null &&
                climbController.CurrentClimbState !=
                Climbing.ClimbController.ClimbState.None)
            {
                EnterClimbing();
            }

            // -------------------------------------------------
            // UTRZYMANIE / WYJŚCIE Z TRYBU
            // -------------------------------------------------
            switch (currentMode)
            {
                case LocomotionMode.Climbing:
                    if (climbController != null &&
                        climbController.CurrentClimbState ==
                        Climbing.ClimbController.ClimbState.None)
                    {
                        ExitClimbing();
                    }
                    break;
            }

            if (!dummy && allowMovement)
            {
                if (currentMode == LocomotionMode.FPP)
                {
                    HandleFPPInput();
                    return;
                }

                if (currentMode == LocomotionMode.TPP)
                {
                    HandleTPPInput();
                    return;
                }

                if (currentMode == LocomotionMode.Climbing)
                {
                    HandleTPPInput();
                    return;
                }
            }
        }


        // =================================================================
        // DETEKCJA PODŁOŻA
        // =================================================================

        private bool OnGround()
        {
            return characterDetection.IsGrounded(stepHeight);
        }


        // =================================================================
        // OBRÓT POSTACI DO KIERUNKU RUCHU
        // =================================================================

        public void RotatePlayer(Vector3 direction)
        {
            float targetAngle =
                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg +
                mainCamera.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothVelocity,
                turnSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // =================================================================
        // UŻYWANE PRZEZ JUMP / PARKOUR / PREDICTION
        // =================================================================

        public Quaternion RotateToCameraDirection(Vector3 direction)
        {
            float targetAngle =
                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg +
                mainCamera.eulerAngles.y;

            return Quaternion.Euler(0f, targetAngle, 0f);
        }

        // =================================================================
        // ZMIANA PRĘDKOŚCI RUCHU
        // =================================================================

        public void ResetMovement()
        {
            characterMovement.ResetSpeed();
        }

        public void ToggleRun()
        {
            if (characterMovement.GetState() != MovementState.Running)
            {
                characterMovement.SetCurrentState(MovementState.Running);
                characterMovement.curSpeed = characterMovement.RunSpeed;
                characterAnimation.animator.SetBool("Run", true);
            }
        }

        public void ToggleWalk()
        {
            if (characterMovement.GetState() != MovementState.Walking)
            {
                characterMovement.SetCurrentState(MovementState.Walking);
                characterMovement.curSpeed = characterMovement.walkSpeed;
                characterAnimation.animator.SetBool("Run", false);
            }
        }

        // =================================================================
        // INFORMACYJNE
        // =================================================================

        public float GetCurrentVelocity()
        {
            return characterMovement.GetVelocity().magnitude;
        }

        // =================================================================
        // BLOKOWANIE / ODBLOKOWANIE KONTROLERA (UŻYWANE PRZEZ DPS)
        // =================================================================

        public void DisableController()
        {
            characterMovement.SetKinematic(true);
            characterMovement.enableFeetIK = false;
            dummy = true;
            allowMovement = false;

            // CLIMB INTEGRATION
            if (climbController != null)
                climbController.enabled = false;
        }

        public void EnableController()
        {
            characterMovement.SetKinematic(false);
            characterMovement.EnableFeetIK();
            characterMovement.ApplyGravity();
            characterMovement.stopMotion = false;
            dummy = false;
            allowMovement = true;

            // CLIMB INTEGRATION
            if (climbController != null)
                climbController.enabled = true;
        }

        // =====================================================================
        // TRYB TPP ⇄ FPP – LOGIKA CENTRALNA
        // =====================================================================

        private void TryEnterFPP()
        {
            // TYLKO z TPP wolno wejść do FPP
            if (currentMode != LocomotionMode.TPP)
                return;

            // Blokady kontekstowe
            if (isVaulting || currentMode == LocomotionMode.Climbing)
                return;

            currentMode = LocomotionMode.FPP;

            switchCameras.FPPCam();

            if (characterVisualRoot != null)
                characterVisualRoot.SetActive(false);
        }

        private void TryExitFPP()
        {
            if (currentMode != LocomotionMode.FPP)
                return;

            currentMode = LocomotionMode.TPP;

            switchCameras.FreeLookCam();

            if (characterVisualRoot != null)
                characterVisualRoot.SetActive(true);
        }


        // =====================================================================
        // HANDLE TPP INPUT – ORYGINALNY DPS
        // =====================================================================
        void HandleTPPInput()
        {
            // -------------------------------------------------------------
            // 1. RUCH – względem kamery (TPP)
            // -------------------------------------------------------------
            Vector3 translation = GroundMovementTPP(characterInput.movement);
            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));

            // -------------------------------------------------------------
            // 2. PRZEŁĄCZANIE CHÓD / BIEG
            // -------------------------------------------------------------
            if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                ToggleRun();
            else
                ToggleWalk();
        }


        // =====================================================================
        // HANDLE FPP INPUT – FIRST PERSON
        // =====================================================================
        void HandleFPPInput()
        {
            // -------------------------------------------------------------
            // 1. OBRÓT POSTACI – TYLKO MYSZ (YAW)
            // -------------------------------------------------------------
            float yaw = characterInput.mouseX * fppMouseSensitivity * Time.deltaTime;

            if (Mathf.Abs(yaw) > 0.0001f)
            {
                Transform target = playModel != null ? playModel : transform;
                target.Rotate(Vector3.up, yaw, Space.World);
            }

            // -------------------------------------------------------------
            // 2. RUCH – STRAFE (A/D) + PRZÓD/TYŁ (W/S)
            //    Bez RotatePlayer(), bez kamery
            // -------------------------------------------------------------
            Vector3 move = GroundMovementFPP(characterInput.movement);
            characterMovement.SetVelocity(Vector3.ClampMagnitude(move, 1.0f));

            // -------------------------------------------------------------
            // 3. ANIMACJE + BIEG
            // -------------------------------------------------------------
            characterAnimation.animator.SetBool(
                "Released",
                move.magnitude <= 0.01f
            );

            if (characterInput.run && characterInput.movement.y > 0.5f)
                ToggleRun();
            else
                ToggleWalk();
        }


        // =====================================================================
        // GROUND MOVEMENT TPP – względem kamery
        // =====================================================================
        Vector3 GroundMovementTPP(Vector2 input)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

            // Pomocnicza kamera ustawiona w osi Y głównej kamery
            freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);

            Vector3 translation =
                freeCamera.forward * input.y +
                freeCamera.right * input.x;

            translation.y = 0;

            if (translation.magnitude > 0)
            {
                RotatePlayer(direction); // obrót postaci do kierunku ruchu
                characterAnimation.animator.SetBool("Released", false);
            }
            else
            {
                ToggleWalk();
                characterAnimation.animator.SetBool("Released", true);
            }

            return translation;
        }

        // =====================================================================
        // GROUND MOVEMENT FPP – lokalny względem postaci
        // =====================================================================
        Vector3 GroundMovementFPP(Vector2 input)
        {
            // Ruch względem aktualnego obrotu postaci
            Vector3 translation =
                transform.forward * input.y +
                transform.right * input.x;

            translation.y = 0f;

            // Animacja "Released"
            characterAnimation.animator.SetBool(
                "Released",
                translation.magnitude <= 0.01f
            );

            return translation;
        }


        public void EnterClimbing()
        {
            previousMode = currentMode;

            // JEŚLI BYLIŚMY W FPP – SPRZĄTAMY RĘCZNIE
            if (previousMode == LocomotionMode.FPP)
            {
                // NIE warunkujemy currentMode
                if (characterVisualRoot != null)
                    characterVisualRoot.SetActive(true);

                if (switchCameras != null)
                    switchCameras.FreeLookCam();
            }

            currentMode = LocomotionMode.Climbing;

            if (switchCameras != null)
                switchCameras.ClimbCam();
        }

        void ExitClimbing()
        {
            currentMode = LocomotionMode.TPP;

            if (switchCameras != null)
                switchCameras.FreeLookCam();
        }


    }
}


*/


namespace Climbing
{
    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(AnimationCharacterController))]
    [RequireComponent(typeof(DetectionCharacterController))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(VaultingController))]

    public class ThirdPersonController : MonoBehaviour
    {
        // Tryby sterowania wysokiego poziomu
        public enum ControlMode
        {
            TPP,
            FPP,
            CLIMB,
            PARKOUR,
            PAUSE
        }

        // Referencje do innych kontrolerów
        [HideInInspector] public InputCharacterController characterInput;
        [HideInInspector] public MovementCharacterController characterMovement;
        [HideInInspector] public AnimationCharacterController characterAnimation;
        [HideInInspector] public DetectionCharacterController characterDetection;
        [HideInInspector] public VaultingController vaultingController;
        [HideInInspector] public Climbing.ClimbController climbController;
        [HideInInspector] public BasicActionController basicActionController;

        // Flagi stanu postaci (oryginał – nie ruszać)
        [HideInInspector] public bool isGrounded = false;
        [HideInInspector] public bool allowMovement = true;
        [HideInInspector] public bool onAir = false;
        [HideInInspector] public bool isJumping = false;
        [HideInInspector] public bool inSlope = false;
        [HideInInspector] public bool isVaulting = false;
        [HideInInspector] public bool dummy = false;

        // Kamery
        [Header("Cameras")]
        public CameraController cameraController;
        public Transform mainCamera;
        public Transform freeCamera;

        // Kamery trybów (aktywowane / dezaktywowane przez TPS)
        public GameObject tppCamera;
        public GameObject fppCamera;
        public GameObject climbCamera;
        //public GameObject parkourCamera;

        [Header("FPP Settings")]
        public Transform fppCameraTransform;       // Referencja do kamery FPP
        public float fppMouseSensitivity = 100f;  // Czułość myszy w FPP

        [Header("TPP Zoom Settings")]
        public Cinemachine.CinemachineFreeLook tppFreeLook;   // przypisz FreeLook dla TPP
        public float zoomSpeed = 1f;
        public float minZoomRadius = 1f;
        public float maxZoomRadius = 6f;
        public float fppTransitionDelay = 1f;  // czas oczekiwania przed wejściem do FPP przy minimalnym zoomie

        // --- Zoom / FPP transition ---
        private bool waitingForFPPTransition = false;
        private float fppTransitionTimer = 0f;

        [Header("Climb Camera")]
        [SerializeField] private CinemachineFreeLook climbFreeLook; // przypisz w inspektorze

        [Header("Climb Zoom Settings")]
        public float climbZoomSpeed = 1f;
        public float climbZoomMinRadius = 1f;
        public float climbZoomMaxRadius = 6f;

        [Header("Parkour Detection")]
        public float parkourDetectionRadius = 5f;
        public LayerMask parkourLayerMask;

        // Aktualny tryb sterowania (debug / inspector)
        [Header("Mode")]
        [SerializeField] public ControlMode currentMode = ControlMode.TPP;

        // Model postaci (ukrywany w FPP)
        [Header("Visual")]
        public GameObject characterVisual;

        [Header("Character Visibility Settings")]
        public float characterHideDelay = 1.7f;   // Opóźnienie znikania postaci w sekundach
        public float characterShowDelay = 0.2f;   // Opóźnienie pojawiania się postaci w sekundach

        private Coroutine characterCoroutine = null;

        // Ustawienia kroku
        [Header("Step Settings")]
        [Range(0, 10.0f)] public float stepHeight = 0.8f;
        public float stepVelocity = 0.2f;

        // Kolidery
        [Header("Colliders")]
        public CapsuleCollider normalCapsuleCollider;
        public CapsuleCollider slidingCapsuleCollider;

        // Parametry obrotu
        private float turnSmoothTime = 0.1f;
        private float turnSmoothVelocity;

        // Debug
        [Header("Debug")]
        public bool enableDebug = true;

        [Header("DebugFLAG")]
        public bool enableDebugFlag = true;

#if UNITY_EDITOR
        [Header("DEBUG STATE WATCHER")]
        public bool debugStateWatcher = true;

        private ControlMode _prevMode;

        private bool _prevIsGrounded;
        private bool _prevOnAir;
        private bool _prevIsJumping;
        private bool _prevIsVaulting;
        private bool _prevAllowMovement;
        private bool _prevDummy;
#endif


        // Inicjalizacja komponentów
        private void Awake()
        {
            characterInput = GetComponent<InputCharacterController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterAnimation = GetComponent<AnimationCharacterController>();
            characterDetection = GetComponent<DetectionCharacterController>();
            vaultingController = GetComponent<VaultingController>();

            // Pobranie ClimbController
            climbController = GetComponent<Climbing.ClimbController>();

            if (climbController == null)
            {
                Debug.LogWarning("ClimbController nie został znaleziony na obiekcie postaci!");
            }

            if (cameraController == null)
                Debug.LogError("Attach the Camera Controller located in the Free Look Camera");

            // Dodanie referencji do BasicActionController
            basicActionController = GetComponent<BasicActionController>();
            if (basicActionController != null)
                basicActionController.SetTPC(this);

        }

        // Podpinanie eventów ruchu
        private void Start()
        {
            characterMovement.OnLanded += characterAnimation.Land;
            characterMovement.OnFall += characterAnimation.Fall;
        }

        // Główna pętla logiki
        void Update()
        {
            // Sprawdzenie podłoża
            isGrounded = OnGround();

            // Maszyna stanów trybów
            UpdateControlMode();

            // sterowanie zależne od trybu
            ControlModeMovement();

            DrawDebug();

#if UNITY_EDITOR
            if (enableDebugFlag)
                DebugStateWatcher();
#endif

        }

        // sterowanie zależne od trybu
        private void ControlModeMovement()
        {

            // Oryginalna logika ruchu TPP + BasicJump
            if (currentMode == ControlMode.TPP)
            {
                HandleTPPZoom();

                if (!dummy && allowMovement)
                {
                    // Ruch poziomy
                    AddMovementInputBasic(characterInput.movement);

                    // Skok – wywołanie BAC
                    if (characterInput.jump && basicActionController != null)
                    {
                        basicActionController.BasicJump();
                    }

                    // Chód/bieg
                    if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                        ToggleRun();
                    else if (!characterInput.run)
                        ToggleWalk();
                }
            }

            // Logika FPP – korzysta z nowych funkcji
            if (currentMode == ControlMode.FPP)
            {
                if (!dummy && allowMovement)
                {
                    AddMovementInputFPP(characterInput.movement);

                    if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                        ToggleRun();
                    else if (!characterInput.run)
                        ToggleWalk();

                    // Obrót postaci w osi X
                    RotatePlayerFPP(characterInput.mouseX);
                }

                // W FPP każdy scroll out powoduje wyjście do TPP
                if (characterInput.scroll < -0.01f)
                {
                    SetMode(ControlMode.TPP);
                }

            }

            // Logika ruchu CLIMB
            if (currentMode == ControlMode.CLIMB)
            {
                HandleClimbZoom();

                if (!dummy && allowMovement)
                {
                    AddMovementInput(characterInput.movement);

                    if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                        ToggleRun();
                    else if (!characterInput.run)
                        ToggleWalk();
                }
            }

            // Logika ruchu PARKOUR
            if (currentMode == ControlMode.PARKOUR)
            {
                HandleParkourZoom(); 
                
                if (!dummy && allowMovement)
                {
                    AddMovementInput(characterInput.movement);

                    if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                        ToggleRun();
                    else if (!characterInput.run)
                        ToggleWalk();
                }
            }

        }

        // Maszyna stanów wysokiego poziomu (Input System ONLY)
        private void UpdateControlMode()
        {
            // Pauza (toggle)
            if (characterInput.pause)
            {
                SetMode(currentMode == ControlMode.PAUSE
                    ? ControlMode.TPP
                    : ControlMode.PAUSE);
                return;
            }

            // Blokada zmian trybu w pauzie
            if (currentMode == ControlMode.PAUSE)
                return;

            // Przełączanie TPP -> FPP
            if (currentMode == ControlMode.TPP && characterInput.enterFPP)
            {
                SetMode(ControlMode.FPP);
                return;
            }

            // Przełączanie FPP -> TPP
            if (currentMode == ControlMode.FPP && characterInput.exitFPP)
            {
                SetMode(ControlMode.TPP);
                return;
            }

            // Przełączenie na CLIMB
            bool startClimb = climbController.toLedge || (climbController.CurrentClimbState != ClimbController.ClimbState.None && climbController.active);
            if (startClimb)
            {
                SetMode(ControlMode.CLIMB);
                return;
            }

            // Wyjście z CLIMB do TPP
            bool endClimb = climbController.CurrentClimbState == ClimbController.ClimbState.None && !climbController.toLedge && !climbController.onLedge;
            if (endClimb && currentMode == ControlMode.CLIMB)
            {
                SetMode(ControlMode.TPP);
                return;
            }
            
            // detekcja sferyczna PARKOUR 
            bool parkourNearby = IsParkourObjectNearby();

            // Wejście do PARKOUR (tylko z TPP i FPP)
            if (currentMode == ControlMode.TPP && parkourNearby || currentMode == ControlMode.FPP && parkourNearby)
            {
                SetMode(ControlMode.PARKOUR);
                return;
            }

            // Wyjście z PARKOUR
            if (currentMode == ControlMode.PARKOUR && !parkourNearby)
            {
                SetMode(ControlMode.TPP);
                return;
            }



        }

        // Zmiana trybu sterowania
        public void SetMode(ControlMode newMode)
        {
            if (currentMode == newMode)
                return;

            ExitMode(currentMode);
            currentMode = newMode;
            EnterMode(newMode);
        }

        // Wejście w tryb
  
        private void EnterMode(ControlMode mode)
        {
            // Domyślnie wyłącz wszystkie kamery
            if (tppCamera != null) tppCamera.SetActive(false);
            if (fppCamera != null) fppCamera.SetActive(false);
            if (climbCamera != null) climbCamera.SetActive(false);
            if (tppCamera != null) tppCamera.SetActive(false);

            switch (mode)
            {
                case ControlMode.TPP:
                    ShowCharacter(true);
                    ForceTPPCameraBehindPlayer();
                    if (tppCamera != null) tppCamera.SetActive(true);
                    break;

                case ControlMode.FPP:
                    ShowCharacter(false);
                    if (fppCamera != null) fppCamera.SetActive(true);
                    break;

                case ControlMode.CLIMB:
                    ShowCharacter(true);
                    if (climbCamera != null) climbCamera.SetActive(true);
                    break;

                case ControlMode.PARKOUR:
                    ShowCharacter(true);
                    ForceTPPCameraBehindPlayer();
                    if (tppCamera != null) tppCamera.SetActive(true);
                    break;

                case ControlMode.PAUSE:
                    allowMovement = false;
                    break;
            }
        }

        // Wyjście z trybu
        private void ExitMode(ControlMode mode)
        {
            if (mode == ControlMode.PAUSE)
                allowMovement = true;
        }

        



        // Sprawdzenie podłoża
        private bool OnGround()
        {
            return characterDetection.IsGrounded(stepHeight);
        }

        // sprawdzanie zasięgu PARKOUR
        private bool IsParkourObjectNearby()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                parkourDetectionRadius,
                parkourLayerMask
            );

            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Vault") ||
                    hit.CompareTag("Deep Jump") ||
                    hit.CompareTag("Slide") ||
                    hit.CompareTag("Reach") ||
                    hit.CompareTag("Pole"))
                {
                    return true;
                }
            }

            return false;
        }


        // Dodanie ruchu PARKOUR/CLIMB
        public void AddMovementInput(Vector2 direction)
        {
            Vector3 translation = GroundMovement(direction);
            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));
        }

        // Ruch dla FPP 
        public void AddMovementInputFPP(Vector2 direction)
        {
            Vector3 translation = GroundMovementFPP(direction);
            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));
        }

        // tryb TPP zwykłe akcje ruchu
        public void AddMovementInputBasic(Vector2 direction)
        {
            Vector3 translation = GroundMovementBasic(direction);

            // Zachowujemy y velocity jeśli postać jest w powietrzu
            if (isJumping || onAir)
            {
                Vector3 currentVel = characterMovement.GetVelocity();
                translation.y = currentVel.y; // nie zerujemy prędkości pionowej
            }

            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));

        }

        // Ruch naziemny zależny od kamery
        Vector3 GroundMovement(Vector2 input)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

            freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);
            Vector3 translation = freeCamera.forward * input.y + freeCamera.right * input.x;
            translation.y = 0;

            if (translation.magnitude > 0)
            {
                RotatePlayer(direction);
                characterAnimation.animator.SetBool("Released", false);
            }
            else
            {
                ToggleWalk();
                characterAnimation.animator.SetBool("Released", true);
            }

            return translation;
        }

        private Vector3 GroundMovementFPP(Vector2 input)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

            // Ruch względem fppCamera
            if (fppCameraTransform != null)
            {
                Vector3 forward = fppCameraTransform.forward;
                Vector3 right = fppCameraTransform.right;

                forward.y = 0;
                right.y = 0;

                Vector3 translation = forward * input.y + right * input.x;

                if (translation.magnitude > 0)
                {
                    characterAnimation.animator.SetBool("Released", false);
                }
                else
                {
                    ToggleWalk();
                    characterAnimation.animator.SetBool("Released", true);
                }

                return translation;
            }

            return Vector3.zero;
        }

        Vector3 GroundMovementBasic(Vector2 input)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

            freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);
            Vector3 translation = freeCamera.forward * input.y + freeCamera.right * input.x;
            translation.y = 0;

            if (translation.magnitude > 0)
            {
                RotatePlayer(direction);
                characterAnimation.animator.SetBool("Released", false);
            }
            else
            {
                ToggleWalk();
                characterAnimation.animator.SetBool("Released", true);
            }

            return translation;
        }


        // Obrót postaci
        public void RotatePlayer(Vector3 direction)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // Obrót postaci w osi Y według ruchu myszy w FPP
        private void RotatePlayerFPP(float mouseX)
        {
            if (mouseX != 0f)
            {
                float angle = mouseX * fppMouseSensitivity * Time.deltaTime; // skalowanie na sensowną prędkość
                transform.Rotate(Vector3.up, angle);
            }
        }




        // Obrót do kierunku kamery (helper)
        public Quaternion RotateToCameraDirection(Vector3 direction)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
            return Quaternion.Euler(0f, targetAngle, 0f);
        }

        // Reset prędkości
        public void ResetMovement()
        {
            characterMovement.ResetSpeed();
        }

        // Przełączenie biegu
        public void ToggleRun()
        {
            if (characterMovement.GetState() != MovementState.Running)
            {
                characterMovement.SetCurrentState(MovementState.Running);
                characterMovement.curSpeed = characterMovement.RunSpeed;
                characterAnimation.animator.SetBool("Run", true);
            }
        }

        // Przełączenie chodu
        public void ToggleWalk()
        {
            if (characterMovement.GetState() != MovementState.Walking)
            {
                characterMovement.SetCurrentState(MovementState.Walking);
                characterMovement.curSpeed = characterMovement.walkSpeed;
                characterAnimation.animator.SetBool("Run", false);
            }
        }

        // Aktualna prędkość
        public float GetCurrentVelocity()
        {
            return characterMovement.GetVelocity().magnitude;
        }

        // Wyłączenie kontrolera
        public void DisableController()
        {
            characterMovement.SetKinematic(true);
            characterMovement.enableFeetIK = false;
            dummy = true;
            allowMovement = false;
        }

        // Włączenie kontrolera
        public void EnableController()
        {
            characterMovement.SetKinematic(false);
            characterMovement.EnableFeetIK();
            characterMovement.ApplyGravity();
            characterMovement.stopMotion = false;
            dummy = false;
            allowMovement = true;
        }

        // Funkcja startująca pojawienie się/ukrycie postaci z opóźnieniem
        private void ShowCharacter(bool value)
        {
            if (characterCoroutine != null)
                StopCoroutine(characterCoroutine);

            characterCoroutine = StartCoroutine(DelayedShowCharacter(value));
        }

        // Coroutine obsługująca opóźnienie
        private IEnumerator DelayedShowCharacter(bool value)
        {
            float delay = value ? characterShowDelay : characterHideDelay;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (characterVisual != null)
                characterVisual.SetActive(value);

            characterCoroutine = null;
        }

        // Debug wizualny
        private void DrawDebug()
        {
            if (!enableDebug)
                return;

            Debug.DrawRay(transform.position, Vector3.down * stepHeight,
                isGrounded ? Color.green : Color.red);
        }

        private void HandleTPPZoom()
        {
            if (tppFreeLook == null || characterInput == null)
                return;

            // Odczyt scrolla myszy
            float scrollValue = characterInput.scroll; // zakładam, że dodałeś scroll w InputCharacterController

            // Pobranie aktualnego promienia środkowej orbity
            float radius = tppFreeLook.m_Orbits[1].m_Radius;

            // --- Zoom in ---
            if (scrollValue > 0.01f)
            {
                if (radius <= minZoomRadius + 0.001f)
                {
                    if (!waitingForFPPTransition)
                    {
                        radius = minZoomRadius;
                        waitingForFPPTransition = true;
                        fppTransitionTimer = fppTransitionDelay;
                    }
                    else
                    {
                        // Jeśli minZoom osiągnięty i użytkownik scrolluje ponownie -> wejście do FPP
                        if (fppTransitionTimer <= 0f)
                        {
                            SetMode(ControlMode.FPP);
                            waitingForFPPTransition = false;
                        }
                    }
                }
                else
                {
                    radius -= scrollValue * zoomSpeed * Time.deltaTime * 50f;
                    waitingForFPPTransition = false;
                }
            }

            // --- Zoom out ---
            if (scrollValue < -0.01f)
            {
                // Każdy scroll out w TPP resetuje timer
                radius -= scrollValue * zoomSpeed * Time.deltaTime * 50f;
                waitingForFPPTransition = false;
            }

            // Timer odliczający delay dla FPP
            if (waitingForFPPTransition)
            {
                fppTransitionTimer -= Time.deltaTime;
                if (fppTransitionTimer < 0f)
                    fppTransitionTimer = 0f;
            }

            // Clamp i zastosowanie do wszystkich orbit
            radius = Mathf.Clamp(radius, minZoomRadius, maxZoomRadius);
            tppFreeLook.m_Orbits[0].m_Radius = radius;
            tppFreeLook.m_Orbits[1].m_Radius = radius;
            tppFreeLook.m_Orbits[2].m_Radius = radius;
        }

        private void HandleClimbZoom()
        {
            if (climbFreeLook == null || characterInput == null)
                return;

            // Odczyt scrolla myszy z InputCharacterController
            float scrollValue = characterInput.scroll;

            // Pobranie aktualnego promienia środkowej orbity
            float radius = climbFreeLook.m_Orbits[1].m_Radius;

            // --- Zoom in ---
            if (scrollValue > 0.01f)
            {
                radius -= scrollValue * climbZoomSpeed * Time.deltaTime * 50f;
            }

            // --- Zoom out ---
            if (scrollValue < -0.01f)
            {
                radius -= scrollValue * climbZoomSpeed * Time.deltaTime * 50f;
            }

            // Clamp i zastosowanie do wszystkich orbit
            radius = Mathf.Clamp(radius, climbZoomMinRadius, climbZoomMaxRadius);
            climbFreeLook.m_Orbits[0].m_Radius = radius;
            climbFreeLook.m_Orbits[1].m_Radius = radius;
            climbFreeLook.m_Orbits[2].m_Radius = radius;
        }

        private void HandleParkourZoom()
        {
            if (tppFreeLook == null || characterInput == null)
                return;

            // Odczyt scrolla myszy z InputCharacterController
            float scrollValue = characterInput.scroll;

            // Pobranie aktualnego promienia środkowej orbity
            float radius = tppFreeLook.m_Orbits[1].m_Radius;

            // --- Zoom in ---
            if (scrollValue > 0.01f)
            {
                radius -= scrollValue * zoomSpeed * Time.deltaTime * 50f;
            }

            // --- Zoom out ---
            if (scrollValue < -0.01f)
            {
                radius -= scrollValue * zoomSpeed * Time.deltaTime * 50f;
            }

            // Clamp i zastosowanie do wszystkich orbit
            radius = Mathf.Clamp(radius, minZoomRadius, maxZoomRadius);
            tppFreeLook.m_Orbits[0].m_Radius = radius;
            tppFreeLook.m_Orbits[1].m_Radius = radius;
            tppFreeLook.m_Orbits[2].m_Radius = radius;
        }

        private void ForceTPPCameraBehindPlayer()
        {
            if (tppFreeLook == null)
                return;

            // 1. Ustaw heading kamery dokładnie jak rotacja postaci
            tppFreeLook.m_XAxis.Value = transform.eulerAngles.y;
            tppFreeLook.m_YAxis.Value = 0.31f;

            // 2. Wymuś minimalny radius (1m – jak pisałeś)
            float radius = minZoomRadius;

            tppFreeLook.m_Orbits[0].m_Radius = radius;
            tppFreeLook.m_Orbits[1].m_Radius = radius;
            tppFreeLook.m_Orbits[2].m_Radius = radius;

            // 3. Opcjonalnie – wyzeruj prędkość osi (żeby CM nie „dokręcał”)
            tppFreeLook.m_XAxis.m_InputAxisValue = 0f;
        }

#if UNITY_EDITOR
        private void DebugStateWatcher()
        {
            bool modeChanged = currentMode != _prevMode;

            bool flagChanged =
                _prevIsGrounded != isGrounded ||
                _prevOnAir != onAir ||
                _prevIsJumping != isJumping ||
                _prevIsVaulting != isVaulting ||
                _prevAllowMovement != allowMovement ||
                _prevDummy != dummy;

            if (modeChanged || flagChanged)
            {
                Debug.Log(
                    $"[TPC STATE] " +
                    $"Mode: {currentMode} | " +
                    $"Grounded: {isGrounded} | " +
                    $"OnAir: {onAir} | " +
                    $"Jumping: {isJumping} | " +
                    $"Vaulting: {isVaulting} | " +
                    $"AllowMove: {allowMovement} | " +
                    $"Dummy: {dummy}"
                );

                // zapamiętaj stan
                _prevMode = currentMode;

                _prevIsGrounded = isGrounded;
                _prevOnAir = onAir;
                _prevIsJumping = isJumping;
                _prevIsVaulting = isVaulting;
                _prevAllowMovement = allowMovement;
                _prevDummy = dummy;
            }
        }
#endif


    }
}
