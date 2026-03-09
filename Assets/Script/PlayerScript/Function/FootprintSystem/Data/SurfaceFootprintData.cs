using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceFootprintData", menuName = "Footprint/Surface Data")]
public class SurfaceFootprintData : ScriptableObject
{
    public string surfaceTag;          // 지면 태그 (예: "Grass", "Snow")
    public GameObject footprintPrefab; // 해당 지면의 발자국 프리팹
    public float lifetime = 5f;        // 발자국 유지 시간
    public float fadeOutDuration = 1f; // 페이드아웃 시간
}
