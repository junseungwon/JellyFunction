# SkinnedMeshRenderer용 하수구 변형 적용 가이드

> **상황:** 캐릭터가 **SkinnedMeshRenderer**(본/리깅)를 사용할 때,  
> 기존 MeshFilter 기반 하수구 변형을 어떻게 바꾸면 되는지 정리한 가이드입니다.

---

## 1. MeshFilter vs SkinnedMeshRenderer 차이

| 구분 | MeshFilter | SkinnedMeshRenderer |
|------|------------|---------------------|
| 메시 소스 | `MeshFilter.sharedMesh` (정적) | 본(Bone)이 매 프레임 버텍스를 변형 |
| `mesh.vertices` 수정 | 수정한 값이 그대로 렌더링됨 | **다음 프레임에 본 연산으로 덮어씌워짐** → 반영 안 됨 |
| 변형 적용 방식 | 같은 메시 버텍스만 수정하면 됨 | **현재 포즈 메시를 추출한 뒤** 그 위에 변형 적용 필요 |

즉, SkinnedMeshRenderer에는 **버텍스를 직접 넣어도 유지되지 않으므로**,  
“한 번 현재 포즈 메시를 **Bake**한 뒤, 그 메시에만 변형을 적용하고, 그 결과를 **별도 MeshFilter로 그리도록**” 바꿔야 합니다.

---

## 2. 전체 전략 요약

```
[평상시]
  SkinnedMeshRenderer ON  → 본 애니메이션으로 표시
  MeshRenderer (변형용) OFF

[진입 시작]
  1) SkinnedMeshRenderer.BakeMesh() 로 현재 포즈 메시 추출
  2) 추출한 버텍스를 "원본(originalVertices)"으로 저장
  3) SkinnedMeshRenderer OFF, 변형용 MeshFilter/MeshRenderer ON

[Entering / Passing / Exiting]
  4) 저장된 원본 버텍스에 기존과 동일한 압축·스트레치 로직 적용
  5) 결과 메시를 변형용 MeshFilter에 넣고 매 프레임 갱신

[복원 완료]
  6) 변형용 MeshRenderer OFF, SkinnedMeshRenderer ON
  7) 변형용 메시는 초기화만 하고, 다음 진입 시 다시 Bake
```

핵심은 **“변형의 기준이 되는 메시”를 SkinnedMesh에서는 Bake로 한 번 만든다**는 점입니다.

---

## 3. 필요한 컴포넌트 구성

- **SkinnedMeshRenderer**  
  - 기존 캐릭터용. 진입 중에는 비활성화.
- **변형 전용 메시 표시용**  
  - **MeshFilter** + **MeshRenderer**  
  - 같은 GameObject에 추가하거나, 자식 오브젝트에 둬도 됨.  
  - 진입 중에만 활성화하고, 여기에 Bake+변형된 메시를 넣음.
- **Material**  
  - SkinnedMeshRenderer와 동일한 머티리얼을 변형용 MeshRenderer에 넣어주면 연출이 맞습니다.  
  - 필요하면 별도 머티리얼 인스턴스로 만들고, SkinnedMeshRenderer는 끈 상태에서만 변형 메시가 보이므로 겹침 이슈는 없습니다.

---

## 4. SkinnedMesh 전용 변형기: MeshDeformerSkinned

기존 `MeshDeformer`와 역할은 같고, **메시 소스**와 **표시 방식**만 SkinnedMesh에 맞춥니다.

- **진입 시작 시**  
  - `SkinnedMeshRenderer.BakeMesh(bakedMesh)`로 현재 포즈 메시를 `bakedMesh`에 채움.  
  - 이 버텍스를 `originalVertices`로 복사해 두고, 이후 변형은 전부 이 배열 기준으로 수행.
- **변형 적용**  
  - 기존 `MeshDeformer`와 동일한 수식으로  
    `originalVertices` → 압축/스트레치 → `currentVertices` → **변형용 MeshFilter.sharedMesh**에 넣음.  
  - `RecalculateNormals()`, `RecalculateBounds()` 호출.
- **표시 전환**  
  - 진입 시작: SkinnedMeshRenderer 비활성화, 변형용 MeshRenderer 활성화.  
  - 복원 완료: 변형용 MeshRenderer 비활성화, SkinnedMeshRenderer 활성화.

이렇게 하면 “MeshFilter가 아니라 SkinnedMeshRenderer를 쓰는 경우”에 대한 변경 사항이 한 군데(MeshDeformerSkinned + 렌더러 전환)로 모입니다.

---

## 5. 컨트롤러 쪽 변경 (DrainSqueezeController)

- **참조**  
  - `MeshDeformer` 대신 **MeshDeformerSkinned**를 참조하도록 바꿉니다.  
  - (공통 인터페이스로 묶고 싶다면 `SetDeformProgress`, `ResetDeform`, 진입 시작/종료 시점만 맞추면 됩니다.)
- **시퀀스**  
  - **진입 시작 직후**: SkinnedMeshRenderer 끄고, 변형용 MeshRenderer 켜기.  
  - **진입 시작 시점에** MeshDeformerSkinned에서 **Bake 한 번** 호출해 원본 버텍스 확보.  
  - Entering/Passing/Exiting 동안은 기존처럼 `SetDeformProgress`만 호출.  
  - **복원 완료 후**: `ResetDeform()` 호출한 뒤, 변형용 MeshRenderer 끄고 SkinnedMeshRenderer 다시 켜기.

즉, “누가 메시를 주는지(MeshFilter vs Bake)”와 “누가 그리는지(SkinnedMeshRenderer vs MeshRenderer)”만 바뀌고,  
진입/통과/복원 타이밍과 `LerpDeform`, `MoveAlongAxis` 로직은 그대로 둡니다.

---

## 5.5 실제 사용 방법 (구현된 스크립트 기준)

이미 **MeshDeformerSkinned.cs**와 **DrainSqueezeController** 수정이 반영되어 있습니다.

### 캐릭터 오브젝트 설정 (SkinnedMesh 사용 시)

1. **캐릭터 루트**에 다음 컴포넌트 부착  
   - `SkinnedMeshRenderer` (기존 캐릭터 메시)  
   - `MeshDeformerSkinned`  
   - `DrainSqueezeController`  
   - `Rigidbody`, `Collider`

2. **MeshDeformerSkinned** Inspector  
   - **Profile**: Create → Deform → DrainSqueezeProfile 으로 만든 에셋 할당  
   - **Deform Mesh Filter** / **Deform Mesh Renderer**: 비워 두면 Awake에서 같은 오브젝트에 자동 추가되고, SkinnedMeshRenderer와 동일한 Material로 설정됨.

3. **DrainSqueezeController** Inspector  
   - **Mesh Deformer**: 비워 둠 (Skinned 사용 시)  
   - **Mesh Deformer Skinned**: 이 캐릭터의 `MeshDeformerSkinned` 컴포넌트 드래그  
   - **Rb**, **Main Collider**, 하수구·타이밍 설정은 기존과 동일.

4. **하수구**: Collider Is Trigger + Tag `DrainEntrance` 는 기존 가이드와 동일.

동작: 트리거 진입 시 `MeshDeformerSkinned.BeginSqueeze()` → Bake 및 렌더러 전환 후, `SetDeformProgress`로 변형 적용, 복원 시 `ResetDeform()`으로 SkinnedMeshRenderer 복원.

---

## 6. 구현 시 체크리스트

- [ ] SkinnedMeshRenderer가 붙은 GameObject에 **MeshFilter + MeshRenderer** 추가 (변형용).  
  → `MeshDeformerSkinned`는 비워 두면 자동 추가함.  
- [ ] 변형용 **MeshRenderer**는 기본 비활성화, **SkinnedMeshRenderer**는 기본 활성화.  
- [ ] 변형용 MeshRenderer에 사용할 **Material** 할당 (SkinnedMesh와 동일 또는 인스턴스).  
- [ ] **MeshDeformerSkinned**에서  
  - `SkinnedMeshRenderer` 참조,  
  - Bake 결과를 받을 **Mesh** 인스턴스 1개 생성 후 재사용,  
  - 변형 결과를 넣을 **MeshFilter** 참조.  
- [ ] **진입 시작 시**  
  - `BakeMesh()` 1회 호출 → `originalVertices` 설정 → SkinnedMeshRenderer OFF, 변형용 MeshRenderer ON.  
- [ ] **복원 완료 시**  
  - 변형용 MeshRenderer OFF, SkinnedMeshRenderer ON.

---

## 7. 주의사항

- **BakeMesh()**  
  - SkinnedMeshRenderer의 **로컬 공간** 기준 메시를 채웁니다.  
  - 변형 로직이 로컬 좌표 기준이면 기존 MeshDeformer와 동일하게 하수구 중심만 `InverseTransformPoint`로 맞추면 됩니다.
- **버텍스 수**  
  - Bake 결과는 원본 SkinnedMesh와 동일하므로, 모바일 등에서는 여전히 버텍스 수 제한을 두는 것이 좋습니다.
- **애니메이션**  
  - 진입 중에는 SkinnedMeshRenderer가 꺼져 있으므로, 그동안 본 애니메이션은 보이지 않습니다.  
  - “진입 중에는 일시정지된 포즈로 찌그러짐”이 기본 동작이라고 보면 됩니다.  
  - 필요하면 진입용 애니메이션을 따로 두고, Bake 시점의 포즈를 그 애니로 맞추는 방식으로 확장할 수 있습니다.

---

## 8. 요약

- **MeshFilter가 아니라 SkinnedMeshRenderer를 쓰는 경우**,  
  `mesh.vertices`를 직접 수정해도 다음 프레임에 본 연산에 의해 덮어씌워지므로, **현재 포즈 메시를 Bake한 뒤 그 메시에만 변형을 적용**해야 합니다.
- **적용 순서**:  
  진입 시작 시 **Bake 1회** → 그 버텍스를 원본으로 저장 → SkinnedMeshRenderer 끄고 변형용 MeshFilter/MeshRenderer로 표시 → 기존과 같은 압축/스트레치 로직 적용 → 복원 후 변형용 끄고 SkinnedMeshRenderer 다시 켜기.
- 위 전략대로 **MeshDeformerSkinned**와 **DrainSqueezeController**의 참조·시퀀스만 바꾸면, 기존 가이드의 “하수구 찌그러짐” 연출을 SkinnedMeshRenderer 캐릭터에도 그대로 적용할 수 있습니다.
