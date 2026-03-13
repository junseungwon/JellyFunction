using UnityEngine;
using CharacterPressing;

/// <summary>
/// 챕터 2-3 전용 스크립트. 타임라인에서 호출할 함수들을 모아둡니다.
/// Inspector에서 ChangeModel 참조를 넣은 뒤, 타임라인 시그널/Animation Event에서 아래 메서드를 호출하면 됩니다.
/// </summary>
public class Jelly_Chapter2_3 : MonoBehaviour
{
    [Header("타임라인 연동")]
    [Tooltip("캐릭터 ↔ 공 전환 담당. 여기에 ChangeModel 컴포넌트가 붙은 오브젝트를 할당하세요.")]
    [SerializeField] private ChangeModel _changeModel = null;

    // ========== 타임라인에서 호출할 함수들 ==========

    /// <summary>캐릭터를 공 모드로 전환합니다. 타임라인 시그널/Animation Event에서 호출.</summary>
    public void TurnIntoBall()
    {
        _changeModel?.ChangeToBall();
    }

    /// <summary>공에서 다시 캐릭터 모드로 복귀합니다. 타임라인 시그널/Animation Event에서 호출.</summary>
    public void RevertToCharacter()
    {
        _changeModel?.ChangeToCharacter();
    }
}
