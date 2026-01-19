# Анализ использования Vector2.y vs Unity Y

## Проблема:

Пользователь спрашивает: "movementDirection.y почему y если y отвечает за верх и низ, мы же так решили?"

## Важное различие:

### Unity координаты (Vector3):
- **X** = влево/вправо (горизонтальная ось)
- **Y** = вверх/вниз (вертикальная ось) ⚠️
- **Z** = вперед/назад (горизонтальная ось)

### Vector2 для SetMovementDirection:
- **x** (первая компонента) = локальный X корабля (влево/вправо)
- **y** (вторая компонента) = локальный Z корабля (вперед/назад) ⚠️ **НЕ Unity Y!**

## Где мы используем:

### 1. Вертикальная скорость (Unity Y):
```csharp
Vector3 shipVelocity = shipController.GetVelocity();
float currentVerticalSpeed = shipVelocity.y; // Unity Y (вверх/вниз) ✓
```

### 2. Горизонтальное движение (локальный Z, хранится в Vector2.y):
```csharp
Vector2 desiredVelocityLocal = new Vector2(
    localHorizontalDelta.x,  // локальный X (влево/вправо)
    localHorizontalDelta.z   // локальный Z (вперед/назад), хранится в y Vector2
);
Vector2 movementDirection = new Vector2(correctionX, correctionZ);
shipController.SetMovementDirection(movementDirection);
// movementDirection.y = локальный Z (вперед/назад), НЕ Unity Y! ✓
```

### 3. В ShipController:
```csharp
public void SetMovementDirection(Vector2 direction)
{
    desiredMovementDirection.x = direction.x; // локальный X (влево/вправо) ✓
    desiredMovementDirection.y = direction.y; // локальный Z (вперед/назад), НЕ Unity Y! ✓
}

float targetAngleX = -desiredMovementDirection.y * maxTiltAngle;
// desiredMovementDirection.y = локальный Z (вперед/назад), НЕ Unity Y! ✓
```

## Вывод:

**Код правильный!** Мы используем:
- `shipVelocity.y` - только для вертикальной скорости (Unity Y)
- `movementDirection.y` / `desiredVelocityLocal.y` - для локального Z (вперед/назад), НЕ Unity Y

**НО:** Это может быть источником путаницы! Название `.y` в Vector2 создает впечатление, что это вертикальная ось, но на самом деле это вторая компонента, которая хранит локальный Z.

## Возможное решение:

Можно создать структуру или использовать более явные имена:
```csharp
struct HorizontalDirection
{
    public float leftRight;    // локальный X
    public float forwardBack;  // локальный Z
}
```

Но это потребует изменения всего кода. Пока что важно просто помнить:
- **Vector2.y** в контексте `SetMovementDirection` = локальный Z (вперед/назад), НЕ Unity Y!
- **Unity Y** (shipVelocity.y) = вертикальная ось (вверх/вниз)

## Проверка:

В коде мы правильно используем:
- ✅ `shipVelocity.y` - только для вертикальной скорости
- ✅ `shipVelocity.x` и `shipVelocity.z` - для горизонтальной скорости
- ✅ `localHorizontalDelta.z` - для локального Z
- ✅ `desiredVelocityLocal.y` - для локального Z (хранится в Vector2.y)
- ✅ `movementDirection.y` - для локального Z (хранится в Vector2.y)

**Все правильно!** Проблема не в путанице между Unity Y и Vector2.y.
