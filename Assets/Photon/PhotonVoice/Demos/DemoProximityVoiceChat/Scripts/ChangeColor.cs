using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(PhotonView))]

public class ChangeColor : MonoBehaviour
{
    private PhotonView photonView;
    public float hueMin, hueMax;
    public float satMin, satMax;
    public float valMin, valMax;

    public void OnLocalPlayerInit()
    {
        photonView = GetComponent<PhotonView>();
        Color random = Random.ColorHSV(hueMin,hueMax,satMin,satMax,valMin,valMax);
        photonView.RPC("ChangeColour", RpcTarget.AllBuffered, new Vector3(random.r, random.g, random.b));
    }

    [PunRPC]
    private void ChangeColour(Vector3 randomColor)
    {
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.SetColor("_Color", new Color(randomColor.x, randomColor.y, randomColor.z));
    }
}
