# Unity 젤리 캐릭터 하수구 진입 — Vertex Deformation 구현 가이드

> **목표:** 3D 젤리 캐릭터가 하수구 모양의 틈새를 비집고 들어가는 연출을  
> 런타임 버텍스 변형(Vertex Deformation) 방식으로 구현한다.

---

## 전체 구조 설계

```
DrainSqueezeController (진입 감지 + 상태 관리)
    └─ MeshDeformer (버텍스 변형 엔진)
        └─ DeformProfile (변형 파라미터 데이터 — ScriptableObject)
```

세 클래스를 분리하면 나중에 "다른 모양의 틈새" 연출도  
`DeformProfile` 하나만 교체해서 재사용할 수 있다.

---

## 전체 동작 흐름

```
OnTriggerEnter("DrainEntrance")
    ↓
[Entering] LerpDeform(0 → 1, 0.6초)
    - 버텍스 XZ 수축 + Y 스트레치
    ↓
[Passing] MoveAlongAxis(Down, 1.2초)
    - X/Z 이동 잠금 + 강제 하강
    ↓
[Exiting] LerpDeform(1 → 0, 0.5초)
    - 버텍스 원형 복원 (볼륨 되돌아옴)
    ↓
ResetDeform() → 완전히 원본 버텍스로 초기화
```

---

## 사전 준비 (필수 설정)

### Unity 에디터 설정

- `Model Import Settings → Read/Write Enabled` ✅ 체크  
  (없으면 런타임에서 `mesh.vertices` 읽기 불가)
- `Optimize Mesh` → **Nothing** (버텍스 순서 보장)  
  ※ 드롭다운에 "Off"가 없으면 **Nothing**을 선택하면 됨.

### 코드 런타임 최적화

```csharp
void Awake() {
    mesh = GetComponent<MeshFilter>().mesh;
    mesh.MarkDynamic(); // 변형용 메시 최적화
}
```

---

## 사용 가이드 (Unity에서 설정하기)

아래 순서대로 설정하면 하수구 진입 연출을 바로 사용할 수 있다.

---

### Step 1. 모델 Import 설정

캐릭터 메시가 들어 있는 3D 모델을 선택한 뒤:

1. **Inspector** → Model 탭에서 **Read/Write Enabled** ✅ 체크  
   (체크하지 않으면 런타임에 `mesh.vertices` 접근 불가)
2. **Optimize Mesh** → **Nothing** 으로 설정 (버텍스 순서 유지).  
   ※ 옵션에 "Off"가 없으면 **Nothing** 선택.

---

### Step 2. DeformProfile 에셋 만들기

1. **Project** 창에서 우클릭 → **Create** → **Deform** → **DrainSqueezeProfile**
2. 생성된 에셋 이름을 정한 뒤(예: `DrainSqueezeDefault`) 선택
3. **Inspector**에서 필요 시 `maxStretchY`(1.8)
   - **영역 설정**: `influenceRadius`(0.8), `coreRadius`(0.2)
   - **커브**: `squeezeCurve` — 기본 EaseInOut 유지해도 됨

---

### Step 3. 캐릭터 오브젝트 설정

#### A. MeshFilter 캐릭터 (일반 메시)

캐릭터 **루트 오브젝트**에 다음 컴포넌트를 붙인다.

| 컴포넌트 | 설명 |
|----------|------|
| **MeshFilter** | 캐릭터 메시 (모델에서 자동 부착된 경우 그대로 사용) |
| **MeshRenderer** | 캐릭터 머티리얼 (기존대로) |
| **MeshDeformer** | Profile 필드에 Step 2에서 만든 DeformProfile 에셋 할당 |
| **Rigidbody** | 물리 이동용 (Kinematic 여부는 게임 설계에 맞게) |
| **Collider** | CapsuleCollider 등 — 트리거 감지용이 아님, 캐릭터 본체용 |
| **DrainSqueezeController** | 아래 참조 연결 |

**DrainSqueezeController** Inspector에서:

- **Mesh Deformer** → 같은 오브젝트의 `MeshDeformer` 드래그
- **Rb** → 같은 오브젝트의 `Rigidbody` 드래그
- **Main Collider** → 같은 오브젝트의 Collider 드래그 (선택)
- **Drain Axis** → 하수구 통과 방향. 세로 하수구면 `(0, -1, 0)` (아래)
- **Enter / Pass / Exit Duration** → 원하는 시간(초). 기본 0.6 / 1.2 / 0.5

#### B. SkinnedMeshRenderer 캐릭터 (리깅 캐릭터)

캐릭터 **루트 오브젝트**에 다음을 붙인다.

| 컴포넌트 | 설명 |
|----------|------|
| **SkinnedMeshRenderer** | 기존 캐릭터 메시·본 애니메이션 |
| **MeshDeformerSkinned** | Profile에 DeformProfile 에셋 할당. Deform Mesh Filter/Renderer는 비워 두면 자동 생성 |
| **Rigidbody** | 위와 동일 |
| **Collider** | 위와 동일 |
| **DrainSqueezeControllerSkinned** | MeshDeformerSkinned·Rb·Main Collider 연결 |

**DrainSqueezeControllerSkinned** Inspector에서:

- **Mesh Deformer Skinned** → 같은 오브젝트의 `MeshDeformerSkinned` 드래그
- **Rb**, **Main Collider**, **Drain Axis**, **타이밍** → MeshFilter 버전과 동일하게 설정

---

### Step 4. 하수구 오브젝트 설정

1. 하수구 **입구**가 될 오브젝트를 준비한다 (빈 오브젝트 또는 하수구 모델의 입구 부분).
2. 해당 오브젝트에 **Collider**를 추가한다 (Box Collider, Sphere Collider 등).
3. Collider에서 **Is Trigger** ✅ 체크.
4. 하수구 오브젝트의 **Tag**를 **`DrainEntrance`** 로 설정한다.  
   - Tag가 없으면: **Edit** → **Project Settings** → **Tags and Layers** → **Tags**에 `DrainEntrance` 추가 후 선택.

---

### Step 5. 동작 확인

1. **Play** 실행.
2. 캐릭터를 하수구 트리거 영역으로 이동시켜 진입시킨다.
3. 다음 순서로 재생되는지 확인한다:
   - **진입(Entering)** — 짧은 시간 동안 찌그러지며 압축
   - **통과(Passing)** — 하수구 축 방향으로 이동 (X/Z 고정)
   - **복원(Exiting)** — 다시 원래 모양으로 복귀

---

### 트러블슈팅

| 현상 | 확인 사항 |
|------|-----------|
| 변형이 안 보임 | 모델 **Read/Write Enabled** 체크 여부, **MeshDeformer**에 Profile 할당 여부 |
| 트리거가 안 걸림 | 하수구 Collider **Is Trigger** 체크, 오브젝트 Tag **DrainEntrance** 여부 |
| Skinned 캐릭터가 깜빡이거나 안 찌그러짐 | **DrainSqueezeControllerSkinned** 사용 여부, **MeshDeformerSkinned**에 Profile 할당 여부 |
| 통과 속도 조절 | `DrainSqueezeController` / `DrainSqueezeControllerSkinned` 내부 `MoveAlongAxis`의 `speed` 값 또는 타이밍(**Pass Duration**) 조절 |

---

## 1. DeformProfile (ScriptableObject)

변형 파라미터를 데이터로 분리해 에디터에서 실시간 조절 가능.

```csharp
[CreateAssetMenu(menuName = "Deform/DrainSqueezeProfile")]
public class DeformProfile : ScriptableObject {

    [Header("압축 강도")]
    public float maxSqueezeX  = 0.35f;
    public float maxSqueezeZ  = 0.35f;
    public float maxStretchY  = 1.8f;

    [Header("영역 설정")]
    public float influenceRadius = 0.8f;
    public float coreRadius      = 0.2f;

    [Header("커브")]
    public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("복원")]
    public float restoreSpeed = 5f;
}
```

---

## 2. MeshDeformer (핵심 변형 엔진)

```csharp
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshDeformer : MonoBehaviour {

    [SerializeField] private DeformProfile profile;

    private Mesh      mesh;
    private Vector3[] originalVertices;
    private Vector3[] workingVertices;
    private Vector3[] currentVertices;

    private float   deformProgress = 0f;
    private Vector3 drainCenter;
    private Vector3 drainAxis;

    void Awake() {
        mesh = GetComponent<MeshFilter>().mesh;
        mesh.MarkDynamic();

        originalVertices = mesh.vertices;
        workingVertices  = new Vector3[originalVertices.Length];
        currentVertices  = new Vector3[originalVertices.Length];

        originalVertices.CopyTo(workingVertices, 0);
    }

    public void SetDeformProgress(float progress, Vector3 worldDrainCenter, Vector3 axis) {
        deformProgress = Mathf.Clamp01(progress);
        drainCenter    = worldDrainCenter;
        drainAxis      = axis.normalized;
        ApplyDeform();
    }

    public void ResetDeform() {
        deformProgress = 0f;
        originalVertices.CopyTo(currentVertices, 0);
        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void ApplyDeform() {
        Vector3 localCenter = transform.InverseTransformPoint(drainCenter);
        Vector3 localAxis   = transform.InverseTransformDirection(drainAxis);
        float curveValue = profile.squeezeCurve.Evaluate(deformProgress);

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 original = originalVertices[i];
            // 거리·영향력 계산
            Vector3 toVertex   = original - localCenter;
            float   axialDist  = Vector3.Dot(toVertex, localAxis);
            Vector3 radialVec  = toVertex - localAxis * axialDist;
            float   radialDist = radialVec.magnitude;

            float influence = 0f;
            if (radialDist < profile.influenceRadius) {
                influence = 1f - Mathf.InverseLerp(
                    profile.coreRadius, profile.influenceRadius, radialDist
                );
                influence = Mathf.Clamp01(influence);
            }

            // XZ 압축 + Y 스트레치
            float   squeezeAmount = curveValue * influence;
            Vector3 deformed      = original;

            if (radialDist > 0.001f) {
                Vector3 radialDir = radialVec / radialDist;
                float   compressX = squeezeAmount * profile.maxSqueezeX;
                float   compressZ = squeezeAmount * profile.maxSqueezeZ;

                deformed.x -= radialDir.x * radialDist * compressX;
                deformed.z -= radialDir.z * radialDist * compressZ;
            }

            float stretchY = 1f + (squeezeAmount * (profile.maxStretchY - 1f));
            deformed += localAxis * (axialDist * (stretchY - 1f));

            currentVertices[i] = deformed;
        }

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
```

---

## 3. DrainSqueezeController (상태 머신 + 진입 감지)

```csharp
using UnityEngine;
using System.Collections;

public class DrainSqueezeController : MonoBehaviour {

    [Header("참조")]
    [SerializeField] private MeshDeformer meshDeformer;
    [SerializeField] private Rigidbody    rb;
    [SerializeField] private Collider     mainCollider;

    [Header("하수구 설정")]
    [SerializeField] private Transform drainTransform;
    [SerializeField] private Vector3   drainAxis = Vector3.down;

    [Header("타이밍")]
    [SerializeField] private float enterDuration = 0.6f;
    [SerializeField] private float passDuration  = 1.2f;
    [SerializeField] private float exitDuration  = 0.5f;

    private enum State { Normal, Entering, Passing, Exiting }
    private State state = State.Normal;

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("DrainEntrance") && state == State.Normal) {
            drainTransform = other.transform;
            StartCoroutine(SqueezeSequence());
        }
    }

    private IEnumerator SqueezeSequence() {
        state = State.Entering;
        yield return StartCoroutine(LerpDeform(0f, 1f, enterDuration)); // 진입 압축

        state = State.Passing;
        LockMovement(true);
        yield return StartCoroutine(MoveAlongAxis(drainAxis, passDuration));
        LockMovement(false); // 통과 후 이동 해제

        state = State.Exiting;
        yield return StartCoroutine(LerpDeform(1f, 0f, exitDuration)); // 복원

        meshDeformer.ResetDeform();
        state = State.Normal;
    }

    private IEnumerator LerpDeform(float from, float to, float duration) {
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t        = Mathf.Clamp01(elapsed / duration);
            float progress = Mathf.Lerp(from, to, t);
            meshDeformer.SetDeformProgress(progress, drainTransform.position, drainAxis);
            yield return null;
        }
    }

    private IEnumerator MoveAlongAxis(Vector3 axis, float duration) {
        float elapsed = 0f;
        float speed   = 1.5f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            rb.MovePosition(rb.position + axis * speed * Time.deltaTime);
            yield return null;
        }
    }

    private void LockMovement(bool locked) {
        rb.constraints = locked
            ? RigidbodyConstraints.FreezePositionX
              | RigidbodyConstraints.FreezePositionZ
              | RigidbodyConstraints.FreezeRotation
            : RigidbodyConstraints.FreezeRotation;
    }
}
```

---

## 파라미터 튜닝 가이드

| 파라미터 | 낮은 값 효과 | 높은 값 효과 | 권장 시작값 |
|---|---|---|---|
| `maxSqueezeX/Z` | 살짝 찌그러짐 | 납작하게 압축 | `0.35` |
| `maxStretchY` | 거의 안 늘어남 | 길게 쭉 늘어남 | `1.8` |
| `influenceRadius` | 좁은 영역만 변형 | 넓게 변형 | `0.8` |
| `coreRadius` | 중심부 변형 부드럽게 | 중심부 변형 딱딱하게 | `0.2` |
| `ente` |rDuration 빠른 진입 | 느린 진입 | `0.6` |
| `passDuration` | 빠른 통과 | 느린 통과 | `1.2` |

---

## 실무 주의사항

| 항목 | 내용 |
|---|---|
| **노멀 재계산 비용** | `RecalculateNormals()`는 비싸므로 상태 전환 시에만 호출 최소화 |
| **다중 캐릭터** | 각 캐릭터마다 `workingVertices` 배열이 독립적으로 있어야 함 (공유 메시 사용 금지) |
| **Skinned Mesh** | 본(Bone) 기반 리깅 캐릭터는 `BakeMesh()`로 현재 포즈 메시를 뽑은 뒤 변형 |
| **모바일 성능** | 버텍스 수 200~300개 이하 권장. 초과 시 `NativeArray + IJobParallelFor`로 전환 |
| **LOD** | 하수구 진입 중에는 고해상도 메시, 평상시엔 LOD1으로 스위칭 권장 |
| **콜라이더** | 통과 구간에서 `CapsuleCollider → SphereCollider(소형)`로 동적 교체 권장 |

---

## 확장 방향

1. **Job System 전환** — 버텍스 수가 많아지면 `IJobParallelFor + Burst Compiler`로 루프 병렬화
2. **Compute Shader 전환** — 고사양 PC/콘솔 타겟 시 GPU에서 버텍스 연산
3. **JellyMesh 결합** — 통과 완료 후 `JellyMesh` 재활성화 + 잔진동 연출 추가
4. **다양한 틈새 형태** — `DeformProfile`을 원형/타원형/십자형 등으로 교체하여 재사용
