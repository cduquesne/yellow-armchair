using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Photon.Voice.Unity;
using System.Linq;

namespace FauteuilJaune
{
    public class RoomManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        [System.Serializable]
        public class RoomDataPackage
        {
            public RoomData[] roomDatas;
        }

        [System.Serializable]
        public class RoomData
        {
            public SlotData[] slotDatas;
        }

        [System.Serializable]
        public class SlotData
        {
            public int friendIndex;
            public int[] focusedFriendsIndexes;
        }

        [System.Serializable]
        public class WhisperDataPackage
        {
            public WhisperData[] whisperData;
        }

        [System.Serializable]
        public class WhisperData
        {
            public int friendIndex = -1;
            public int whisperingToIndex = -1;
        }


        public AudioMixer mainMixer;
        public AudioMixerGroup sameRoomMixer;
        public AudioMixerGroup otherRoomMixer;
        public AudioMixerGroup focusRoomMixer;
        public float snapshotInterpolationTime = 0.2f;
        public AnimationCurve occlusionToLowpassFrequency = new AnimationCurve();
        public float distanceSumOcclusionFactor = 0.5f;
        public float distanceSumDistanceFactor = 0.5f;
        public Room[] rooms;
        public AcousticPortal[] portals;
        
        public Room defaultRoom;
        public Friend localFriend;
        public List<Friend> allFriends = new List<Friend>();
        public static RoomManager instance;
        public static void PingInstance()
        {
            instance = GameObject.Find("Rooms").GetComponent<RoomManager>();
        }
        public const byte REFRESH_EVENT = 2;
        public const byte NAME_REFRESH_EVENT = 3;
        public const byte WHISPER_EVENT = 4;

        private void Awake()
        {
            instance = this;
        }

        void Update()
        {
            UpdateAudio();
        }

        // Start is called before the first frame update
        public void InitLocalFriend(Friend _friend)
        {
            localFriend = _friend;
            _friend.InitAsLocal();
            defaultRoom.LocalFriendMoveTo();
            
            Destroy(Camera.main.gameObject.GetComponent<AudioListener>());
        }

        public void LocalFriendMove(Slot _slot)
        {
            if (localFriend.currentSlot.room != _slot.room)
            {
                localFriend.currentSlot.room.StopAmbiance();
                _slot.room.PlayAmbiance();
            }

            localFriend.MoveTo(_slot);

            BreakFocusLinks();
            BroadcastRoomData();
            UpdateAudio();
            OrientListener();


            _slot.room.acousticSnapshot.TransitionTo(snapshotInterpolationTime);
        }

        void BreakFocusLinks()
        {
            foreach(Friend f in allFriends)
            {
                if(f.focused && f.currentSlot.room != localFriend.currentSlot.room)
                {
                    localFriend.Focus(f, false);
                }
                if(f.focusedBy && f.currentSlot.room != localFriend.currentSlot.room)
                {
                    f.Focus(localFriend, false);
                }
            }
        }

        void BroadcastRoomData()
        {
            Debug.Log("Broadcast Room Data : " + localFriend.friendIndex);
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions();
            raiseEventOptions.Receivers = ReceiverGroup.Others;
            SendOptions sendOptions = new SendOptions();
            string param = JsonUtility.ToJson(GetRoomDatas());
            PhotonNetwork.RaiseEvent(REFRESH_EVENT, param as object, raiseEventOptions, sendOptions);
        }

        public static void BroadcastNameRefreshEvent()
        {
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions();
            raiseEventOptions.Receivers = ReceiverGroup.All;
            SendOptions sendOptions = new SendOptions();
            PhotonNetwork.RaiseEvent(NAME_REFRESH_EVENT, null, raiseEventOptions, sendOptions);
        }

        void BroadcastWhisperEvent()
        {
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions();
            raiseEventOptions.Receivers = ReceiverGroup.Others;
            SendOptions sendOptions = new SendOptions();
            string param = JsonUtility.ToJson(GetWhisperDatas());
            PhotonNetwork.RaiseEvent(WHISPER_EVENT, param as object, raiseEventOptions, sendOptions);
        }

        void OrientListener()
        {
            Vector3 centerOfInterest = Vector3.zero;
            int friendCount = 0;
            
            var r = localFriend.currentSlot.room;
            if (r.currentFriends.Count > 0)
            {
                foreach (Friend f in r.currentFriends)
                {
                    if (f != localFriend)
                    {
                        friendCount++;
                        centerOfInterest += f.transform.position;
                    }
                }
            }

            if(friendCount == 0)
            {
                centerOfInterest = r.transform.position;
            }
            else
            {
                centerOfInterest /= friendCount;
            }
            localFriend.transform.forward = (centerOfInterest - localFriend.transform.position).normalized;
        }

        RoomDataPackage GetRoomDatas()
        {
            RoomDataPackage pck = new RoomDataPackage();
            RoomData[] roomDatas = new RoomData[rooms.Length];
            for(int i = 0; i < rooms.Length; i++)
            {
                roomDatas[i] = new RoomData();
                roomDatas[i].slotDatas = new SlotData[rooms[i].slots.Length];
                for(int j = 0; j < rooms[i].slots.Length; j++)
                {
                    roomDatas[i].slotDatas[j] = new SlotData();
                    Friend f = rooms[i].slots[j].friend;
                    if(f != null)
                    {
                        roomDatas[i].slotDatas[j].friendIndex = f.friendIndex;
                        roomDatas[i].slotDatas[j].focusedFriendsIndexes = new int[f.focusedFriends.Count];
                        for(int k = 0; k < f.focusedFriends.Count; k++)
                        {
                            roomDatas[i].slotDatas[j].focusedFriendsIndexes[k] = f.focusedFriends[k].friendIndex;
                        }
                    }
                    else
                    {
                        roomDatas[i].slotDatas[j].friendIndex = -1;
                    }
                }
            }
            pck.roomDatas = roomDatas;
            return pck;
        }

        void SetRoomDatas(RoomDataPackage _roomDataPackage)
        {
            ClearFocus();
            RoomData[] datas = _roomDataPackage.roomDatas;
            for(int i = 0; i < datas.Length;i++)
            {
                for(int j = 0; j < datas[i].slotDatas.Length; j++)
                {
                    SlotData slotData = datas[i].slotDatas[j];
                    var friend = GetFriend(slotData.friendIndex);//friend = présent sur le slot
                    if(friend)//y'a qqn ?
                    {
                        friend.MoveTo(rooms[i].slots[j]);//on déplace
                        foreach (int friendIndex in slotData.focusedFriendsIndexes)//il focuse qui lui ?
                        {
                            Friend f = GetFriend(friendIndex);
                            if(f != null)
                            {
                                friend.Focus(f, true);
                            }
                        }
                        
                    }
                }
            }
        }

        void ClearFocus()
        {
            foreach(Friend f in allFriends)
            {
                f.LocalSetFocus(false);
                f.LocalSetFocusBy(false);
                f.focusedFriends.Clear();
            }
        }

        void UpdateAudio()
        {
            foreach(Friend f in allFriends)
            {
                if(localFriend.currentSlot && f.currentSlot && localFriend.currentSlot.room == f.currentSlot.room)
                {
                    f.audioSource.volume = 1f;
                    f.audioFilter.cutoffFrequency = 24000f;
                    f.audioSource.outputAudioMixerGroup = sameRoomMixer;
                    f.audioSource.transform.localPosition = Vector3.zero;
                    if(f.focusedBy || f.focused)
                    {
                        f.audioSource.outputAudioMixerGroup = focusRoomMixer;
                    }
                }
                else
                {
                    f.audioSource.volume = 0.7f;
                    if(localFriend && localFriend.currentSlot)
                    {
                        var path = localFriend.currentSlot.room.pathLibrary[f.currentSlot.room];
                        f.audioFilter.cutoffFrequency = occlusionToLowpassFrequency.Evaluate(path.occlusionSum + path.distanceSum * distanceSumOcclusionFactor);
                        Vector3 localFriendPos = localFriend.transform.position;
                        var dist = Vector3.Distance(f.transform.position, localFriendPos) + path.distanceSum * distanceSumDistanceFactor;
                        f.audioSource.transform.position = (path.repositioning - localFriendPos).normalized * dist + localFriendPos;
                        f.audioSource.outputAudioMixerGroup = otherRoomMixer;
                    }
                    
                }

                if (f.whisperingTo != null)
                {
                    if (f.whisperingTo == localFriend)
                        f.audioSource.outputAudioMixerGroup = focusRoomMixer;
                    else
                        f.audioSource.volume = 0f;
                }
            }
        }

        WhisperDataPackage GetWhisperDatas()
        {
            WhisperDataPackage pck = new WhisperDataPackage();
            WhisperData[] whisperDatas = new WhisperData[allFriends.Count];
            for(int i = 0; i < whisperDatas.Length; i++)
            {
                whisperDatas[i] = new WhisperData();
                whisperDatas[i].friendIndex = allFriends[i].friendIndex;
                whisperDatas[i].whisperingToIndex = allFriends[i].whisperingTo ? allFriends[i].whisperingTo.friendIndex : -1;
            }
            pck.whisperData = whisperDatas;
            return pck;
        }

        void SetWhisperDatas(WhisperDataPackage _whisperDataPackage)
        {
            ClearWhisper();
            WhisperData[] datas = _whisperDataPackage.whisperData;
            for (int i = 0; i < datas.Length; i++)
            {
                Friend f = GetFriend(datas[i].friendIndex);
                if(f)
                {
                    Friend target = GetFriend(datas[i].whisperingToIndex);
                    if(target)
                    {
                        f.Whisper(target, true);
                    }
                }
            }
        }

        void ClearWhisper()
        {
            foreach (Friend f in allFriends)
            {
                f.whisperingTo = null;
                f.LocalStopWhisper();
            }
        }

        Friend GetFriend(int _index)
        {
            return allFriends.Find(a => a.friendIndex == _index);
        }

        public void OnEvent(EventData _eventData)
        {
            if(_eventData.Code == REFRESH_EVENT)
            {
                Debug.Log("Receive REFRESH_EVENT");
                SetRoomDatas(JsonUtility.FromJson<RoomDataPackage>(_eventData.CustomData as string));
                UpdateAudio();
                OrientListener();
            }
            else if( _eventData.Code == NAME_REFRESH_EVENT)
            {
                List<Player> players = PhotonNetwork.PlayerList.ToList();
                foreach (var p in players)
                {
                    foreach(Friend f in allFriends)
                    {
                        if(f.friendIndex == p.ActorNumber)
                        {
                            f.UpdateName(p.NickName);
                        }
                    }
                }
            }
            else if (_eventData.Code == WHISPER_EVENT)
            {
                SetWhisperDatas(JsonUtility.FromJson<WhisperDataPackage>(_eventData.CustomData as string));
                UpdateAudio();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if(PhotonNetwork.IsMasterClient)
            {
                BroadcastRoomData();
                //BroadcastNameRefreshEvent();
            }
        }

        public void OnFriendDestroyed(Friend _friend)
        {
             allFriends.RemoveAll(a => a == _friend);
             foreach(Room r in rooms)
             {
                 r.currentFriends.RemoveAll(a => a == _friend);
             }
             foreach(Friend f in allFriends)
            {
                f.focusedFriends.Remove(_friend);
            }
        }

        public void FriendClick(Friend _friend)
        {
            if (localFriend != _friend
                && localFriend.currentSlot
                && _friend.currentSlot
                && localFriend.currentSlot.room == _friend.currentSlot.room)
            { 
                localFriend.Focus(_friend, !localFriend.focusedFriends.Contains(_friend));
                BroadcastRoomData();
            }
        }

        public void FriendWhisper(Friend _friend)
        {
            if (localFriend != _friend
               && localFriend.currentSlot
               && _friend.currentSlot
               && localFriend.currentSlot.room == _friend.currentSlot.room)
            {
                localFriend.Whisper(_friend, true);
                BroadcastWhisperEvent();
            }
        }

        public void FriendStopWhisper(Friend _friend)
        {
            if (localFriend != _friend)
            {
                localFriend.Whisper(_friend, false);
                BroadcastWhisperEvent();
            }
        }

        [ContextMenu("Bake Portal Paths")]
        private void BakePortalPaths()
        {
            portals = GetComponentsInChildren<AcousticPortal>();
            foreach(Room listenerRoom in rooms)
            {
                BakePortalPathsForRoom(listenerRoom);
            }
        }

        public void BakePortalPathsForRoom(Room _listenerRoom)
        {
            _listenerRoom.acousticPaths.Clear();
            foreach (Room emitterRoom in rooms)
            {
                if (_listenerRoom != emitterRoom)
                {
                    List<object> path = new List<object>();
                    var success = RecursiveRoomSearch(emitterRoom, _listenerRoom, ref path);

                    if (success)//Store path
                    {
                        AcousticPortal lastPortal = null;
                        AcousticPath acousticPath = new AcousticPath();
                        acousticPath.emitterRoom = emitterRoom;
                        acousticPath.listenerRoom = _listenerRoom;
                        foreach (object o in path)
                        {
                            AcousticPortal portal = o as AcousticPortal;
                            if (portal)
                            {
                                if (lastPortal)
                                {
                                    acousticPath.distanceSum += Vector3.Distance(lastPortal.transform.position, portal.transform.position);
                                }
                                acousticPath.occlusionSum += portal.occlusion;
                                lastPortal = portal;
                            }
                        }
                        if (lastPortal)
                            acousticPath.repositioning = lastPortal.transform.position;
                        _listenerRoom.acousticPaths.Add(acousticPath);
                    }
                }
            }
        }

        bool RecursiveRoomSearch(Room _startRoom, Room _targetRoom, ref List<object> _roomPath)
        {
            List<object> roomPath = new List<object>(_roomPath);

            foreach(AcousticPortal portal in portals)
            {
                if (roomPath.Contains(portal))
                    continue;
                if(portal.roomA == _startRoom)//portail valide
                {
                    if(portal.roomB == _targetRoom) //chemin terminé !
                    {
                        roomPath.Add(_startRoom);
                        roomPath.Add(portal);
                        _roomPath = roomPath;
                        return true;
                    }
                    else if(!roomPath.Contains(portal.roomB))
                    {
                        roomPath.Add(_startRoom);
                        roomPath.Add(portal);
                        bool success = RecursiveRoomSearch(portal.roomB, _targetRoom, ref roomPath);
                        if(success)
                        {
                            _roomPath = roomPath;
                            return true;
                        }     
                        else
                        {
                            roomPath.Remove(portal);
                            roomPath.Remove(_startRoom);
                        }
                    }
                }
                else if (portal.roomB == _startRoom)//portail valide
                {
                    if (portal.roomA == _targetRoom) //chemin terminé !
                    {
                        roomPath.Add(_startRoom);
                        roomPath.Add(portal);
                        _roomPath = roomPath;
                        return true;
                    }
                    else if (!roomPath.Contains(portal.roomA))
                    {
                        roomPath.Add(_startRoom);
                        roomPath.Add(portal);
                        bool success = RecursiveRoomSearch(portal.roomA, _targetRoom, ref roomPath);
                        if (success)
                        {
                            _roomPath = roomPath;
                            return true;
                        } 
                        else
                        {
                            roomPath.Remove(portal);
                            roomPath.Remove(_startRoom);
                        }
                    }
                }
            }
            return false;
        }
    }
}


