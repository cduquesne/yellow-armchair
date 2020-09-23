using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FauteuilJaune
{
    public class Room : MonoBehaviour
    {
        public Slot[] slots;
        public List<Friend> currentFriends = new List<Friend>(20);
        public List<AcousticPath> acousticPaths = new List<AcousticPath>(10);
        public Dictionary<Room, AcousticPath> pathLibrary = new Dictionary<Room, AcousticPath>();
        public string acousticSnapshotName;
        public AudioMixerSnapshot acousticSnapshot;
        private AudioSource ambianceAudioSource;
        private bool playSound;
        private float baseVolume;
        public float fadeDuration = 5f;


        // Start is called before the first frame update
        [ContextMenu("RefreshSlotsAndRooms")]
        void RefreshEditor()
        {
            slots = GetComponentsInChildren<Slot>();
        }

        [ContextMenu("OrientSlotsTowardsCenter")]
        void OrientSlotsTowardsCenter()
        {
            foreach(Slot s in slots)
            {
                s.transform.forward = (transform.position - s.transform.position).normalized;
                //s.transform.up = Vector3.up;
            }
        }

        [ContextMenu("BakePathsForRoom")]
        void BakePathsForRoom()
        {
            RoomManager.PingInstance();
            RoomManager.instance.BakePortalPathsForRoom(this);
        }

        [ContextMenu("InitSnapshotNames")]
        void InitSnapshotNames()
        {
            acousticSnapshotName = name;
        }


        private void Awake()
        {
            ambianceAudioSource = GetComponent<AudioSource>();
            if(ambianceAudioSource)
                baseVolume = ambianceAudioSource.volume;

            foreach (Slot s in slots)
            {
                s.room = this;
            }
            foreach(AcousticPath p in acousticPaths)
            {
                pathLibrary.Add(p.emitterRoom, p);
            }
        }

        void OnMouseDown()
        {
            if(RoomManager.instance.localFriend)
                LocalFriendMoveTo();
        }

        public void LocalFriendMoveTo()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].friend == null)
                {
                    RoomManager.instance.LocalFriendMove(slots[i]);
                    break;
                }
            }
        }

        public void PlayAmbiance()
        {
            if (!ambianceAudioSource)
                return;

            if(!playSound)
            {
                playSound = true;
                ambianceAudioSource.Play();
            }
            
        }

        public void StopAmbiance()
        {
            if (!ambianceAudioSource)
                return;
            playSound = false;
        }

        private void Update()
        {

            if(ambianceAudioSource)
            {
                if (playSound)
                {
                    if (ambianceAudioSource.volume < baseVolume)
                        ambianceAudioSource.volume += Time.deltaTime * baseVolume * (1f / fadeDuration);

                    if (ambianceAudioSource.volume > baseVolume)
                    {
                        ambianceAudioSource.volume = baseVolume;
                    }
                }
                else if (!playSound)
                {
                    if (ambianceAudioSource.volume > 0f)
                    {
                        ambianceAudioSource.volume -= Time.deltaTime * baseVolume * (1f / fadeDuration);
                    }
                    else
                    {
                        ambianceAudioSource.Stop();
                    }
                }
            }
            
        }
    }
}


