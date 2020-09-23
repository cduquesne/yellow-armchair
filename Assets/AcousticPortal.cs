using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FauteuilJaune
{
    [System.Serializable]
    public class AcousticPath
    {
        public Room emitterRoom;
        public Room listenerRoom;
        public float occlusionSum;
        public float distanceSum;
        public Vector3 repositioning;
    }

    public class AcousticPortal : MonoBehaviour
    {
        public Room roomA;
        public Room roomB;
        public float occlusion;
    }
}

