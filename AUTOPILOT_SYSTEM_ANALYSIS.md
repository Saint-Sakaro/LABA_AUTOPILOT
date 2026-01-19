# Анализ системы автопилота

## 1. Как автопилот определяет куда лететь

### Вычисление направления к цели:
1. **Мировые координаты:**
   - `worldDelta = targetPosition - shipPosition`
   - `worldDirectionToTarget = (worldDelta.x, 0, worldDelta.z)` - только горизонтальная компонента
   - Нормализуется: `worldDirectionToTarget.Normalize()`

2. **Преобразование в локальные координаты:**
   - `localDirectionToTarget = shipTransform.InverseTransformDirection(worldDirectionToTarget)`
   - Это дает направление к цели в локальной системе координат корабля

3. **Передача в SetMovementDirection:**
   - `localDirection = (localDirectionToTarget.x, localDirectionToTarget.z)`
   - `SetMovementDirection(localDirection)`
   - Где: `x` = влево/вправо (локальный X), `y` = вперед/назад (локальный Z)

## 2. Как летит корабль (физика)

### Применение силы от двигателей (`ApplyThrustFromEngines`):

1. **Для каждого двигателя:**
   - `engineForward = engine.transform.forward` - направление двигателя
   - `engineDirection = -engineForward` - направление силы (противоположно forward)
   - `force = engineDirection * thrustForce` - сила в мировых координатах

2. **Если двигатели повернуты (`enginesTilted == true`):**
   - Все силы суммируются: `totalForce += force` для каждого двигателя
   - Сила применяется к центру масс: `shipRigidbody.AddForce(totalForce)`
   - **Результат:** Только перемещение, БЕЗ вращения

3. **Если двигатели НЕ повернуты (`enginesTilted == false`):**
   - Сила применяется к позиции двигателя: `AddForceAtPosition(force, enginePosition)`
   - Вычисляется вращающий момент: `torque = Cross(leverArm, force)`
   - **Результат:** Перемещение + вращение

## 3. Как управляются двигатели

### A. Управление тягой (`SetThrust`):
- `currentThrust` устанавливается для всех двигателей
- `engineThrusts[i] = currentThrust` для всех i
- Используется для контроля вертикальной скорости

### B. Управление направлением (`SetMovementDirection`):
1. **Установка желаемого направления:**
   - `desiredMovementDirection.x = direction.x` (влево/вправо)
   - `desiredMovementDirection.y = direction.y` (вперед/назад)

2. **Вычисление углов поворота (`UpdateEngineRotationsFromMovementDirection`):**
   ```
   targetAngleX = -desiredMovementDirection.y * maxTiltAngle
   targetAngleY = -desiredMovementDirection.x * maxTiltAngle
   ```
   
   **Интерпретация:**
   - Если `y > 0` (вперед) → `targetAngleX < 0` (наклон назад) → двигатель наклоняется назад → forward направлен назад → сила вперед ✓
   - Если `x > 0` (вправо) → `targetAngleY < 0` (поворот влево) → двигатель поворачивается влево → forward направлен влево → сила вправо ✓

3. **Применение поворота (`UpdateEngineRotationsSmoothly`):**
   - `xRotation = Quaternion.AngleAxis(deltaX, shipRight)` - поворот вокруг правой оси
   - `yRotation = Quaternion.AngleAxis(deltaY, transform.up)` - поворот вокруг верхней оси
   - `targetRotation = baseRotation * yRotation * xRotation`
   - Двигатель плавно поворачивается к `targetRotation`

## 4. Проблема

### Из логов:
- Корабль: (-2.836, 764.979, 103.118)
- Цель: (-0.197, 0.000, -0.001)
- Разница: (2.639, -764.979, -103.118)
- Мировое направление: (0.026, 0.000, -1.000) - почти полностью назад!
- Локальное направление: (0.026, 0.000, -1.000) - совпадает с мировым

### Проблема:
Цель находится почти прямо под кораблем (разница по X = 2.6м, по Z = -103м), но направление получается почти полностью назад (-1.000 по Z), потому что компонент Z намного больше компонента X.

### Почему это проблема:
Если мы передаем `(0.026, -1.000)` в `SetMovementDirection`:
- `desiredMovementDirection.x = 0.026` (немного вправо)
- `desiredMovementDirection.y = -1.000` (полностью назад)
- `targetAngleX = -(-1.000) * maxTiltAngle = +maxTiltAngle` (наклон вперед)
- `targetAngleY = -0.026 * maxTiltAngle` (почти ноль, немного вправо)

Двигатели наклоняются вперед, их forward направлен вперед, сила направлена назад → корабль улетает назад от цели!

## 5. Решение

Проблема в том, что мы используем нормализованное направление, которое при большом Z-компоненте становится почти полностью назад. 

**Правильный подход:**
- Если цель почти прямо под кораблем (малое горизонтальное расстояние), мы должны компенсировать только горизонтальную скорость, а не пытаться двигаться к цели по направлению.
- Или использовать ненормализованное направление, взвешенное по расстоянию.
