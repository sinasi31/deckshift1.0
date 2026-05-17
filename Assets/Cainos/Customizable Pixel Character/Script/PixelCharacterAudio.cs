using Cainos.CustomizablePixelCharacter;
using UnityEngine;
using Cainos.LucidEditor;

//character audio placeholder
//if you are going to add audio to the character, try extending from this script

namespace Cainos.CustomizablePixelCharacter
{
    public class PixelCharacterAudio : MonoBehaviour
    {
        [FoldoutGroup("Audio")] public AudioClip audioFootstep;
        [FoldoutGroup("Audio")] public AudioClip audioFoostepHeavy;
        [Space]
        [FoldoutGroup("Audio")] public AudioClip audioAttack;
        [FoldoutGroup("Audio")] public AudioClip audioBowPull;
        [FoldoutGroup("Audio")] public AudioClip audioBowShoot;
        [Space]
        [FoldoutGroup("Audio")] public AudioClip audioJump;
        [FoldoutGroup("Audio")] public AudioClip audioThrow;
        [FoldoutGroup("Audio")] public AudioClip audioSwim;
        [FoldoutGroup("Audio")] public AudioClip audioDodgeStart;
        [Space]
        [FoldoutGroup("Audio")] public AudioClip audioUnderwaterBubble;


        [FoldoutGroup("Params")] public float volumeFootstepWalk = 0.7f;
        [FoldoutGroup("Params")] public float volumeFootstepRun = 1.0f;
        [FoldoutGroup("Params")] public float volumeFootstepCrouch = 0.4f;
        [FoldoutGroup("Params")] public float volumeFootstepCrawl = 0.2f;
        [FoldoutGroup("Params")] public float volumeFootstepLadder = 0.7f;

        private PixelCharacterController Controller
        {
            get
            {
                if (controller == null) controller = GetComponentInParent<PixelCharacterController>();
                return controller;
            }
        }
        private PixelCharacterController controller;

        private PixelCharacter Character
        {
            get
            {
                if (character == null) character = GetComponent<PixelCharacter>();
                return character;
            }
        }
        private PixelCharacter character;

        private void Awake()
        {
            Controller.onFootstep.AddListener(OnFootstep);
            Controller.onJump.AddListener(OnJump);
            Controller.onLand.AddListener(OnLand);

            Controller.onBowPull.AddListener(OnBowPull);
            Controller.onBowShoot.AddListener(OnBowShoot);
            Controller.onThrow.AddListener(OnThrow);

            Controller.onSwimHand.AddListener(OnSwim);

            Controller.onDodgeStart.AddListener(OnDodgeStart);
            Controller.onDodgeEnd.AddListener(OnDodgeEnd);

            Controller.onAttackStart.AddListener(OnAttackStart);

            Controller.onBubble.AddListener(OnBubble);
        }


        //handle footstep
        private void OnFootstep()
        {
            if (Controller.IsDead) return;

            //ladder
            if (Controller.IsClimbingLadder || Controller.IsEnteringLadder || Controller.IsExitingLadder)
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepLadder);
                return;
            }

            //ledge
            if (Controller.IsClimbingLedge)
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepWalk);
                return;
            }

            //crawling
            if (Controller.IsCrawling)
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepCrawl);
                return;
            }

            //crouching
            if (Controller.IsCrouching)
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepCrouch);
                return;
            }


            //run
            if (Controller.IsRunning || Controller.IsDashing)
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepRun);
                return;
            }
            //walk
            else
            {
                PlayAudio(audioFootstep, transform.position, volumeFootstepWalk);
                return;
            }
        }

        private void OnJump()
        {
            if (Controller.IsDead) return;

            PlayAudio(audioFoostepHeavy, transform.position, 1.0f);
            PlayAudio(audioJump, transform.position, 1.0f);
        }

        private void OnLand()
        {
            PlayAudio(audioFoostepHeavy, transform.position, 1.0f);
        }

        private void OnBowPull()
        {
            PlayAudio(audioBowPull, transform.position, 1.0f);
        }

        private void OnBowShoot()
        {
            PlayAudio(audioBowShoot, transform.position, 1.0f);
        }

        private void OnDodgeStart()
        {
            if (Controller.IsDead) return;

            PlayAudio(audioDodgeStart, transform.position, 1.0f);
        }

        private void OnDodgeEnd()
        {
        }

        private void OnThrow()
        {
            PlayAudio(audioThrow, transform.position, 1.0f);
        }

        private void OnSwim()
        {
            PlayAudio(audioSwim, transform.position, 1.0f);
        }

        private void OnAttackStart()
        {
            if (Controller.IsDead) return;

            PlayAudio(audioAttack, transform.position, 1.0f);
        }

        private void OnBubble()
        {
            PlayAudio(audioUnderwaterBubble, transform.position, 1.0f);
        }

        private void PlayAudio(AudioClip audio, Vector3 worldPos, float volume = 1.0f)
        {
            if (audio == null) return;
            AudioSource.PlayClipAtPoint(audio, worldPos, volume);
        }
    }
}
