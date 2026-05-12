# SpineRagdoll 동작 원리
LLM을 위해 만든 문서이지만 그냥 원리가 궁금하면 보셔도 됩니다
## 개요

Spine skeleton의 뼈(bone)를 직접 제어해 래그돌을 구현한다.  
몸통은 **Verlet 위치 적분**, 팔다리는 **Verlet 각도 스프링**으로 구동되며,  
두 시스템은 `smoothAccel`(몸통 가속도)을 매개로 연결된다.

---

## 전체 흐름

```
초기 속도 설정 (Verlet mainPrev 역산)
    ↓
매 프레임:
  몸통 Verlet 이동 → 충돌 처리
  smoothAccel 계산 (가속도 변화 추적)
  팔다리 스프링 적분 (wobbleVel → wobbleRot)
  update_skeleton() 호출
    └→ [시그널] 뼈 회전 주입
         채찍 연쇄 (부모 wob * 0.4)
         lag Lerp (깊이별 추종 속도)
    ↓
MaxTime 경과 or 정지 조건 → 페이드아웃 후 제거
```

---

## 단계별 설명

### 1단계 — 초기 속도 계산 (23~28줄)

```csharp
float angleRad = RagdollAngleDirectionDeg + 랜덤 스프레드
var dir        = new Vector2(cos(angle), sin(angle))   // 날아갈 방향 단위벡터
float speed    = RagdollSpeed + overDamage * 30f       // 초기 속도
var mainPrev   = mainPos - dir * speed * 0.016f        // "0.016초 전 위치" 역산
```

Verlet 적분에서 **현재 위치 − 이전 위치 = 속도**다.  
`mainPrev`를 직접 역산해 초기 속도를 부여한다.

---

### 2단계 — 뼈 계층 파악 (51~100줄)

| 자료구조 | 내용 |
|---|---|
| `boneObjects` | 이름 → 뼈 오브젝트 |
| `boneParents` | 이름 → 부모 이름 |
| `boneDepth` | 이름 → 루트로부터의 깊이 |
| `rootBoneName` | 부모가 없는 유일한 뼈 |

Spine skeleton의 뼈 트리를 C# 딕셔너리로 미러링해 둔다.  
이 정보를 **채찍 연쇄 효과**와 **lag 효과**에 사용한다.

---

### 3단계 — wobbleRot / wobbleVel 초기화 (102~110줄)

각 뼈(루트 제외)마다 스프링 상태를 초기화한다.

```csharp
float depthScale = 1f + boneDepth[name] * 0.3f          // 말단 뼈일수록 더 크게 쏠림
wobbleRot[name] = (dir.X * 12f + dir.Y * 6f) * depthScale   // X·Y 방향 모두 반영
wobbleVel[name] = (dir.X * 2.5f + dir.Y * 1.2f) * AngularSpeed * depthScale
                + 랜덤 노이즈                                 // 뼈마다 조금씩 다르게
```

| 변수 | 의미 | 물리 비유 |
| --- | --- | --- |
| `wobbleRot` | 회전 변위 (각도 오프셋) | 스프링 늘어난 길이 |
| `wobbleVel` | 회전 속도 | 스프링 끝의 속도 |
| `depthScale` | 깊이 보정 계수 (1 + depth * 0.3) | 말단 뼈일수록 더 크게 초기화 |

`dir.x`와 `dir.y`를 반영해 초기 속도와 가속도를 정한다.


---

### 4단계 — 몸통 Verlet 적분 (196~200줄)

```csharp
vel      = (mainPos - mainPrev) * airDamp   // 현재 속도 = 위치 차분
mainPrev = mainPos
mainPos += vel + gravity * dt²              // 다음 위치 = 현재 + 속도 + 중력
```

Verlet 적분은 가속도를 명시적으로 저장하지 않고 이전 위치만으로 속도를 암묵적으로 관리한다.  
`airDamp`를 곱해 공기 저항을 흉내낸다.

---

### 5단계 — 충돌 처리 (210~232줄)

**바닥**
```csharp
if (mainPos.Y > floorY)
    mainPos.Y = floorY, mainPrev.Y = floorY  // 수직 속도 0으로 고정
```

**좌우 벽**
```csharp
vx         = mainPos.X - mainPrev.X         // 충돌 직전 수평 속도
mainPrev.X = mainPos.X + vx * BounceDamp    // 반전 + 감쇠 → 다음 프레임에 반사
bodyAngVel = -bodyAngVel * BounceDamp       // 텀블링도 반전
```

---

### 6단계 — 팔다리 스프링 물리 (235~247줄)

매 프레임 각 뼈에 대해 **가속도에 반응하는 감쇠 스프링**을 적분한다.

```csharp
// 1. 몸통 가속도가 팔다리를 끌어당김 (관성 효과)
wobbleVel -= smoothAccel.X * df * 3.0f
wobbleVel -= smoothAccel.Y * df * 1.5f

// 2. 스프링 복원력 (변위에 비례해 원래 각도로 돌아옴)
wobbleVel -= wobbleRot * SpringK * df * dt

// 3. 감쇠 (에너지 소산)
wobbleVel *= WobbleDamp^(dt*60)

// 4. 적분
wobbleRot += wobbleVel * dt
```

`df`는 뼈 계층 깊이에 따라 커지므로, 말단 뼈일수록 가속도에 더 민감하게 반응한다.  
몸통이 급격히 방향을 바꾸면 팔다리가 관성으로 뒤처졌다가 진동하며 돌아온다.

---

### 7단계 — 뼈 회전 적용 (158~176줄)

`update_skeleton()` 호출 시 `before_world_transforms_change` 시그널이 발생하고,  
핸들러 안에서 각 뼈의 실제 회전을 주입한다.

```csharp
target = baseRot[name] + wobbleRot[name]           // 기준각 + 스프링 변위
       + parentWob * 0.4f                           // 부모 변위의 40% 추가 전달

followSpeed = clamp(1.0 - depth * 0.15, 0.3, 1.0)  // 깊을수록 느리게 따라옴
currentRot  = Lerp(currentRot, target, followSpeed)
bone.set_rotation(currentRot)
```

**채찍 효과**: 부모 wob → 자식으로 40% 전달 → 손자로 또 40% → 말단 뼈가 가장 크게 흔들린다.  
**lag 효과**: Lerp의 t값을 깊이로 줄여서 말단 뼈는 목표 각도에 천천히 도달한다.

---

### 8단계 — 텀블링 (206~207줄)

```csharp
bodyAngVel *= BodyDamp^(dt*60)   // 감쇠
bodyAngle   = bodyAngVel * dt    // 이번 프레임 회전각
rootBone.rotation += bodyAngle   // 루트 뼈에 직접 적용
```

몸통 전체가 날아가면서 회전한다. 벽 충돌 시 반전된다.

---

## 주요 상수

| 상수 | 값 | 역할 |
|---|---|---|
| `AirDamp` | 0.99 | 공기 저항 (위치) |
| `BounceDamp` | 0.4 | 벽 충돌 반발 감쇠 |
| `BodyDamp` | 0.972 | 텀블링 감쇠 |
| `WobbleDamp` | 0.98 | 팔다리 스프링 감쇠 |
| `SpringK` | 0.1 | 스프링 강성 |
| `MaxTime` | 3.0s | 래그돌 최대 지속 시간 |
| `StopVelSq` | 1.0 | 정지 판정 속도² (무중력 전용) |
| `StopAngDeg` | 0.5° | 정지 판정 각속도 (무중력 전용) |
