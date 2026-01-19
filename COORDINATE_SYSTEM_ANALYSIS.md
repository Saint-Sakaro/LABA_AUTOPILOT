# Анализ системы координат и управления

## Проблема из логов:

**Начальная скорость:** X=0.07, Z=0.05 (почти ноль)
**После автопилота:** X=1.07, Z=0.32 (корабль улетает вправо и вперед)
**Направление к цели:** X=7.48 (вправо), Z=-0.67 (сзади)

**desiredVelocityLocal:** x=2.24 (вправо), y=-0.20 (назад)
**currentVelocityLocal:** x=0.07 (вправо), y=0.05 (вперед)
**correctionZ=-0.077** (назад) - правильно, так как цель сзади

**НО:** Корабль движется вперед (Z увеличивается), хотя correctionZ отрицательный!

## Цепочка преобразований:

### 1. Вычисление направления к цели:
- `worldHorizontalDelta: X=7.48, Z=-0.67` (цель вправо и сзади)
- `localHorizontalDelta: X=7.48, Z=-0.67` (в локальных координатах)
- `desiredVelocityLocal: x=2.24, y=-0.20` (y = локальный Z = -0.20, значит назад) ✓

### 2. PID коррекция:
- `correctionZ = -0.077` (назад) ✓

### 3. SetMovementDirection:
- `movementDirection.y = -0.077` (назад) ✓
- Передается в `SetMovementDirection(0.660, -0.077)`

### 4. UpdateEngineRotationsFromMovementDirection:
```csharp
targetAngleX = -desiredMovementDirection.y * maxTiltAngle;
// Если desiredMovementDirection.y = -0.077 (назад)
// targetAngleX = -(-0.077) * maxTiltAngle = +0.077 * maxTiltAngle (наклон вперед)
```
- Если двигатель наклоняется вперед, его `forward` направлен вперед
- `engineDirection = -engineForward` (сила направлена назад) ✓

### 5. ApplyThrustFromEngines:
```csharp
Vector3 engineDirection = -engineForward; // Thrust is opposite to engine's forward direction
Vector3 force = engineDirection * thrustForce;
```
- Если `engineForward` направлен вперед, `engineDirection` направлен назад
- `force` направлена назад ✓
- Применяется к центру масс: `AddForce(totalForce)`

## КРИТИЧЕСКИЕ ВОПРОСЫ ДЛЯ ПРОВЕРКИ:

### 1. Нужны логи из ShipController!
В логах пользователя НЕТ информации о:
- Углах поворота двигателей (`targetAngleX`, `targetAngleY`)
- Направлении `engineForward` каждого двигателя
- Направлении `totalForce` (локальные и мировые координаты)

**Нужно добавить логирование в ShipController:**
- Когда `UpdateEngineRotationsFromMovementDirection()` вызывается с `desiredMovementDirection.y = -0.077`
- Какие углы вычисляются (`targetAngleX`, `targetAngleY`)
- В каком направлении поворачивается каждый двигатель (`engineForward`)
- В каком направлении создается `totalForce` (локальные координаты)

### 2. Проверка знаков в коде:

**UpdateEngineRotationsFromMovementDirection:**
```csharp
targetAngleX = -desiredMovementDirection.y * maxTiltAngle;
targetAngleY = -desiredMovementDirection.x * maxTiltAngle;
```
- Если `desiredMovementDirection.y < 0` (назад) → `targetAngleX > 0` (наклон вперед) ✓
- Если `desiredMovementDirection.x > 0` (вправо) → `targetAngleY < 0` (поворот влево) ✓

**ApplyThrustFromEngines:**
```csharp
Vector3 engineDirection = -engineForward;
```
- Если `engineForward` направлен вперед → `engineDirection` направлен назад ✓

### 3. Возможная проблема:

**Может быть проблема в том, что `engineForward` уже направлен назад?**
- Если двигатель изначально направлен назад (вниз), то:
  - `engineForward` направлен назад
  - `engineDirection = -engineForward` направлен вперед
  - Сила создается вперед ✗

**Нужно проверить:**
- Какое начальное направление `engineForward` у двигателей?
- Направлены ли двигатели вниз (backward) или вверх (forward)?

### 4. Проверка координат:

**В Unity:**
- X = влево/вправо
- Y = вверх/вниз
- Z = вперед/назад

**В SetMovementDirection:**
- `direction.x` = локальный X (влево/вправо)
- `direction.y` = локальный Z (вперед/назад, НЕ Unity Y!)

**Проверка:**
- `localHorizontalDelta.z = -0.67` (цель сзади) ✓
- `desiredVelocityLocal.y = -0.20` (назад, это локальный Z) ✓
- `movementDirection.y = -0.077` (назад, это локальный Z) ✓

## НАЧАЛЬНОЕ НАПРАВЛЕНИЕ ДВИГАТЕЛЕЙ:

Из кода видно:
```csharp
// В Awake():
Vector3 localEuler = engines[i].transform.localEulerAngles;
initialEngineRotations[i] = new Vector2(normalizedX, normalizedY);
initialEngineRotationsQuat[i] = engines[i].transform.localRotation;
```

**КРИТИЧЕСКИЙ ВОПРОС:**
- В какую сторону направлен `engine.transform.forward` по умолчанию?
- Если двигатели направлены вниз (для создания тяги вверх), то:
  - `engineForward` направлен вниз (backward в локальных координатах корабля)
  - `engineDirection = -engineForward` направлен вверх (forward в локальных координатах)
  - Сила создается вверх ✓

- Если двигатели направлены вверх (для создания тяги вниз), то:
  - `engineForward` направлен вверх (forward в локальных координатах)
  - `engineDirection = -engineForward` направлен вниз (backward в локальных координатах)
  - Сила создается вниз ✗ (неправильно для посадки)

**НУЖНО ПРОВЕРИТЬ:**
- В Unity Inspector: какое `transform.forward` у двигателей?
- В логах при старте: `Debug.Log($"Двигатель {i}: направление {engines[i].transform.forward}")`

## ПЛАН ДЕЙСТВИЙ:

1. **Включить логирование в ShipController:**
   - Убедиться, что `showDebugInfo = true` в ShipController
   - Проверить, что логи из `UpdateEngineRotationsFromMovementDirection()` и `ApplyThrustFromEngines()` появляются

2. **Проверить начальное направление двигателей:**
   - В Unity Inspector посмотреть `transform.forward` каждого двигателя
   - В логах при старте посмотреть начальное направление
   - Вычислить, в какую сторону создается сила по умолчанию

3. **Проверить логи после включения автопилота:**
   - Когда `desiredMovementDirection.y = -0.077` (назад):
     - Какой `targetAngleX` вычисляется?
     - В какую сторону поворачивается двигатель?
     - Какое `engineForward` после поворота?
     - Какое `totalForce` (локальные координаты)?

4. **Сравнить ожидаемое и фактическое:**
   - Если `desiredMovementDirection.y < 0` (назад), ожидаем:
     - `targetAngleX > 0` (наклон вперед)
     - `engineForward` направлен вперед (локальный Z > 0)
     - `engineDirection` направлен назад (локальный Z < 0)
     - `totalForce.z < 0` (назад) ✓

## ВОЗМОЖНОЕ РЕШЕНИЕ:

**Если проблема в знаке:**
- Если `engineForward` изначально направлен вниз (backward), то логика правильная ✓
- Если `engineForward` изначально направлен вверх (forward), то нужно изменить знак:
  - В `UpdateEngineRotationsFromMovementDirection`: `targetAngleX = desiredMovementDirection.y * maxTiltAngle` (убрать минус)
  - Или в `ApplyThrustFromEngines`: `Vector3 engineDirection = engineForward` (убрать минус)

**НО:** Не менять код до получения полной информации из логов!
