using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FauteuilJaune
{
    public class Slot : MonoBehaviour
    {
        public Friend friend;
        public Room room;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}


