# Jellyfier 사용 가이드

메시 버텍스를 변형시켜 **젤리처럼 출렁거리는** 효과를 주는 스크립트 사용 방법입니다.

---

## 1. 필요한 컴포넌트

| 컴포넌트 | 설명 |
|----------|------|
| **MeshFilter** | 변형할 메시 (캐릭터/오브젝트 메시) |
| **MeshRenderer** | 메시 렌더링 (MeshFilter와 함께 보통 자동 부착) |
| **Jellyfier** | 젤리 물리 + 앞뒤 출렁거림 적용 |

일반 3D 메시 오브젝트(리깅 없는 모델)에 사용합니다.  
**리깅된 캐릭터**는 `JellyfierSkinned`를 사용하세요.

---

## 2. 설정 방법

### Step 1. 모델 Import 설정

1. 프로젝트에서 메시가 들어 있는 3D 모델 선택
2. **Inspector** → Model 탭에서 **Read/Write Enabled** 체크  
   (체크하지 않으면 런타임에 버텍스 접근 불가)

### Step 2. 오브젝트에 스크립트 부착

1. 씬에서 젤리로 만들 메시 오브젝트 선택
2. **Add Component** → **Jellyfier** 검색 후 추가
3. 같은 오브젝트에 **MeshFilter**가 있는지 확인 (모델 추가 시 보통 있음)

### Step 3. 파라미터 조절

Inspector에서 아래 값들을 목적에 맞게 조절합니다.

---

## 3. 파라미터 설명

### 기본 물리

| 파라미터 | 설명 | 권장 |
|----------|------|------|
| **Bounce Speed** | 원래 위치로 되돌아가는 속도 (스프링 강도) | 50~200 |
| **Fall Force** | 충돌 시 가해지는 힘 (출렁거림 세기) | 25~80 |
| **Stiffness** | 속도 감쇠 (클수록 빨리 멈춤) | 1~10 |

### Wobble (앞뒤 출렁거림)

충돌 시 **앞→뒤→앞→뒤**로 흔들리게 하는 부분입니다.

| 파라미터 | 설명 | 권장 |
|----------|------|------|
| **Wobble Direction** | 흔들리는 축 (로컬 기준). 기본 `(0, 0, 1)` = 앞뒤 | Vector3.forward |
| **Wobble Strength** | 출렁거림 세기 배율 | 0.5~2 |
| **Wobble Frequency** | 앞뒤 한 번 흔들리는 속도 (클수록 빠름) | 3~6 |
| **Wobble Decay** | 흔들림이 줄어드는 속도 (클수록 빨리 잔잔해짐) | 3~8 |
| **Idle Wobble Strength** | 충돌 없이도 계속 흔들리는 세기. 0이면 꺼짐 | 0 = 꺼짐, 10~30 = 부드럽게 |

---

## 4. 동작 방식

1. **Awake**  
   - `MeshFilter`의 메시에서 버텍스 복사  
   - 원본(initial), 현재(current), 속도(velocity) 배열 준비  

2. **Update**  
   - **Wobble**: `Sin(phase)`로 앞(+1) / 뒤(-1) 방향을 번갈아 가하며 속도에 가산.  
     **Idle Wobble Strength**가 0보다 크면 충돌 없이도 이 값만큼 계속 흔들림.  
   - **스프링**: 현재 위치와 원본 위치 차이만큼 속도를 줄여 원형으로 복귀  
   - **감쇠**: stiffness로 속도 감소  
   - 버텍스 갱신 후 `RecalculateBounds/Normals/Tangents` 호출  

3. **OnCollisionEnter**  
   - 다른 콜라이더와 충돌 시 `wobbleImpulse`에 `fallForce`만큼 가산  
   - 이 값이 Wobble 쪽에서 앞뒤 흔들림 세기로 사용되고, `wobbleDecay`로 서서히 감소  

---

## 5. 충돌로 출렁거리게 하려면

- 젤리 오브젝트에 **Rigidbody** + **Collider** (Capsule, Sphere, Box 등) 부착  
- 충돌할 대상에도 **Collider** 부착 (Is Trigger 여부는 게임 설계에 맞게)  
- 충돌이 일어나면 자동으로 `OnCollisionEnter`가 호출되어 앞뒤 출렁거림이 시작됩니다.

---

## 6. 스크립트로 힘 주기 (선택)

마우스 클릭 등으로 특정 지점에 힘을 주고 싶다면:

```csharp
// 월드 좌표 _point에 _pressure 만큼 힘 적용
jellyfier.ApplyPressureToPoint(_point, _pressure);
```

- `_point`: 힘을 줄 위치 (월드 좌표)
- `_pressure`: 힘 크기 (예: 50~100)

거리 역제곱으로 영향이 줄어들어, 가까운 버텍스일수록 더 많이 움직입니다.

---

## 7. 트러블슈팅

| 현상 | 확인 사항 |
|------|------------|
| 변형이 안 보임 | 모델 **Read/Write Enabled** 체크 여부 |
| 충돌해도 안 흔들림 | **Rigidbody** + **Collider** 부착 여부, 충돌 상대에도 Collider 있는지 |
| 너무 많이/적게 흔들림 | **Fall Force**, **Wobble Strength**, **Wobble Frequency** 조절 |
| 너무 오래/빨리 잔잔해짐 | **Wobble Decay**, **Stiffness** 조절 |
| 다른 축으로 흔들리게 하고 싶음 | **Wobble Direction**을 (1,0,0) 또는 (0,1,0) 등으로 변경 |

---

## 8. Skinned 메시(리깅 캐릭터) 사용 시

본 애니메이션이 있는 캐릭터는 **JellyfierSkinned**를 사용하세요.

- **SkinnedMeshRenderer**가 있는 오브젝트에 **JellyfierSkinned** 추가
- 매 프레임 본 포즈를 베이크한 뒤, 그 위에 같은 방식으로 젤리 + 앞뒤 출렁거림 적용
- 사용 가능한 파라미터는 Jellyfier와 동일하게 조절하면 됩니다.
