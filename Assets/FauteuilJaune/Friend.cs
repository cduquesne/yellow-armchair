using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Voice.Unity;
using System.Linq;

namespace FauteuilJaune
{
    public class Friend : MonoBehaviour
    {
        public bool debug = false;
        public Slot currentSlot = null;
        public GameObject focusIcon = null;
        public GameObject focusByIcon = null;
        public int friendIndex;
        public PhotonView photonView;
        public AudioSource audioSource;
        public AudioLowPassFilter audioFilter;
        public UnityEngine.UI.Text nameText;
        public LineRenderer whisperLine;
        public Vector3 whisperLineOffset;
        public float whisperLineMargin = 0.5f;
        bool listenerInitialized = false;
        public List<Friend> focusedFriends = new List<Friend>();
        public Friend whisperingTo = null;
        public float whisperTimeThreshold = 0.3f;
        private bool justWhispered = false;
        private float whisperTimer =0f;
        private bool isClickingOnFriend = false;

        //local variables
        public bool focused = false;
        public bool focusedBy = false;
        // Start is called before the first frame update

        private void Awake()
        {
            if(PhotonNetwork.InRoom)
            {

            }
            photonView = PhotonView.Get(this);
            audioSource = GetComponentInChildren<AudioSource>();
            audioFilter = GetComponentInChildren<AudioLowPassFilter>();
            MoveTo(RoomManager.instance.defaultRoom.slots[0]);
        }

        private void Start()
        {
            RoomManager.instance.allFriends.Add(this);
        }

        private void Update()
        {
            if(isClickingOnFriend)
            {
                whisperTimer += Time.deltaTime;
                if(whisperTimer > whisperTimeThreshold)
                {
                    RoomManager.instance.FriendWhisper(this);
                    justWhispered = true;
                }
            }
        }

        public void InitAsLocal()
        {
            PhotonView.Get(this).RPC("SetFriendIndex", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);
            var listener = gameObject.AddComponent<AudioListener>();
            GameObject.Find("Voice").GetComponent<WebRtcAudioDsp>().SetOrSwitchAudioListener(listener);
            var changeColors = GetComponentsInChildren<ChangeColor>();
            foreach(var changeColor in changeColors)
            {
                changeColor.OnLocalPlayerInit();
            }
            RoomManager.BroadcastNameRefreshEvent();
            Destroy(Camera.main.gameObject.GetComponent<AudioListener>());
            listenerInitialized = true;
        }

        private void OnDestroy()
        {
            RoomManager.instance.OnFriendDestroyed(this);
        }

        private void OnMouseUpAsButton()
        {
            if(!justWhispered)
                RoomManager.instance.FriendClick(this);
        }

        private void OnMouseDown()
        {
            
            isClickingOnFriend = true;
            justWhispered = false;
            whisperTimer = 0f;
        }

        private void OnMouseUp()
        {
            isClickingOnFriend = false;
            if (justWhispered)
            {
                RoomManager.instance.FriendStopWhisper(this);
            }
        }

        public void MoveTo(Slot _slot)
        {
            if(currentSlot)
            {
                currentSlot.friend = null;
                currentSlot.room.currentFriends.Remove(this);
                if(this == RoomManager.instance.localFriend && currentSlot.room != _slot.room)
                    currentSlot.room.StopAmbiance();
            }
                
            _slot.friend = this;
            currentSlot = _slot;
            this.transform.position = _slot.transform.position;
            this.transform.rotation = _slot.transform.rotation;
            _slot.room.currentFriends.Add(this);
            if (this == RoomManager.instance.localFriend)
                _slot.room.PlayAmbiance();
        }

        public void Focus(Friend _friend, bool _state)
        {
            
            if(_state)
            {
                if (debug)
                {
                    Debug.Log(this + "focused" + _friend);
                }
                if(!focusedFriends.Contains(_friend))
                    focusedFriends.Add(_friend);
                Debug.Log(this + " Added friend " + _friend);
                if(this == RoomManager.instance.localFriend)
                {
                    _friend.LocalSetFocus(true);
                }
                if(_friend == RoomManager.instance.localFriend)
                {
                    this.LocalSetFocusBy(true);
                }
            }
            else
            {
                if (debug)
                {
                    Debug.Log(this + "unfocused" + _friend);
                }
                focusedFriends.RemoveAll(a => a == _friend);
                if (this == RoomManager.instance.localFriend)
                {
                    _friend.LocalSetFocus(false);
                }
                if(_friend == RoomManager.instance.localFriend)
                {
                    this.LocalSetFocusBy(false);
                }
            }
        }

        public void Whisper(Friend _friend, bool _state)
        {
            if(_state)
            {
                whisperingTo = _friend;
                if(_friend == RoomManager.instance.localFriend)
                    LocalStartWhisper(_friend);
                if(this == RoomManager.instance.localFriend)
                    LocalStartWhisper(_friend);
            }
            else
            {
                whisperingTo = null;
                if (_friend == RoomManager.instance.localFriend)
                    LocalStopWhisper();
                if (this == RoomManager.instance.localFriend)
                    LocalStopWhisper();
            }
        }

        public void LocalSetFocus(bool _state)
        {
            if (debug)
            {
                Debug.Log(this + " : Local focus = " + _state);
            }
            focused = _state;
            focusIcon.SetActive(focused);
        }

        public void LocalSetFocusBy(bool _state)
        {
            if (debug)
            {
                Debug.Log(this + " : Local focus BY = " + _state);
            }
            focusedBy = _state;
            focusByIcon.SetActive(focusedBy);
        }

        public void LocalStartWhisper(Friend _target)
        {
            whisperLine.gameObject.SetActive(true);
            Vector3 toTarget = (_target.transform.position - transform.position).normalized;
            whisperLine.SetPosition(0, transform.position - whisperLineOffset + toTarget * whisperLineMargin);
            whisperLine.SetPosition(1, _target.transform.position - _target.whisperLineOffset - toTarget * whisperLineMargin);
        }

        public void LocalStopWhisper()
        {
            whisperLine.gameObject.SetActive(false);
        }

        public void UpdateName(string _name)
        {
            name = _name;
            nameText.text = _name;
        }

        [PunRPC]
        void SetFriendIndex(int _index)
        {
            friendIndex = _index;
        }

        public override string ToString()
        {
            return "Friend" + friendIndex + "("+gameObject.name+")";
        }
    }

}
