using UnityEngine;
using Unity.Cinemachine;
using Unity.VisualScripting;

public class CinemachineCameraChange : MonoBehaviour
{
    public CinemachineCamera camA;
    public CinemachineCamera camB;

    public void SwitchToB()
    {
        camA.gameObject.SetActive(false);
        camB.gameObject.SetActive(true);
        // Brain이 자동으로 Blend 처리
    }

    void OnTriggerEnter(Collider other)
    {

        SwitchToB();

    }
}
