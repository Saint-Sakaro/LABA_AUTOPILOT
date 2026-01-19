# Подробное объяснение систем проекта

## 1. ВЕТЕР (Wind)

### Физика ветра:
Ветер - это **поток воздуха**, который создает силу давления на поверхность корабля. В реальной физике:
- Сила ветра зависит от **площади поверхности** корабля, на которую дует ветер
- Ветер создает **линейную силу** (толкает корабль) и **момент вращения** (крутит корабль)
- **Центр давления** (точка приложения силы) зависит от формы корабля и направления ветра

### Как работает в коде:

**Файл:** `ShipController.cs`, метод `ApplyWind()`

```csharp
// 1. Вычисляем направление и силу ветра
Vector3 windForce = new Vector3(
    horizontalX * horizontalForceStrength,  // Горизонтальная сила X
    verticalForceStrength,                  // Вертикальная сила Y
    horizontalZ * horizontalForceStrength  // Горизонтальная сила Z
);

// 2. Вычисляем центр давления (где ветер "давит" на корабль)
Vector3 centerOfPressure = CalculateCenterOfPressure(windDirectionLocal);

// 3. Применяем силу к кораблю через Rigidbody
shipRigidbody.AddForceAtPosition(windForce, centerOfPressure, ForceMode.Force);
```

**Принципы:**
- **ForceMode.Force** - постоянная сила (как в реальной физике)
- **AddForceAtPosition** - применяет силу в конкретной точке, что создает вращение
- **Центр давления** вычисляется на основе:
  - Размера корабля (shipSize)
  - Направления ветра (какая сторона корабля "ловит" ветер)
  - Площади поверхности (frontBackArea, leftRightArea, topBottomArea)

**Почему корабль крутится:**
- Если центр давления **не совпадает** с центром масс → создается момент вращения
- Формула: `Torque = Cross(leverArm, force)` где `leverArm = centerOfPressure - centerOfMass`

---

## 2. ТУРБУЛЕНТНОСТЬ (Turbulence)

### Физика турбулентности:
Турбулентность - это **нерегулярные, хаотичные движения воздуха**, которые:
- Создают **случайные силы** (толкают корабль в разные стороны)
- Создают **случайные моменты вращения** (крутят корабль)
- В реальности турбулентность больше влияет на **вращение** (крен, тангаж, рысканье), чем на линейное движение

### Как работает в коде:

**Файл:** `TurbulenceZone.cs` - зона турбулентности

**Генерация силы:**
```csharp
// Используем Perlin Noise для плавной случайности
float noiseX = Mathf.PerlinNoise(localPos.x * 0.1f + time, noiseSeed * 0.1f) * 2f - 1f;
float noiseY = Mathf.PerlinNoise(localPos.y * 0.1f + time, ...) * 2f - 1f;
float noiseZ = Mathf.PerlinNoise(localPos.z * 0.1f + time, ...) * 2f - 1f;

Vector3 noiseDirection = new Vector3(noiseX, noiseY, noiseZ).normalized;
float forceMagnitude = turbulenceStrength * maxForceMagnitude;
return noiseDirection * forceMagnitude;
```

**Генерация момента вращения:**
```csharp
// Разделяем на три оси вращения (реалистично)
float rollNoise = ...;   // Крен (X) - самый сильный
float pitchNoise = ...;  // Тангаж (Y) - средний
float yawNoise = ...;    // Рысканье (Z) - слабый

Vector3 torqueAxis = new Vector3(
    rollNoise * 1.2f,    // Крен усилен
    pitchNoise * 0.9f,   // Тангаж
    yawNoise * 0.7f      // Рысканье ослаблен
);
```

**Применение в ShipController:**
```csharp
// Получаем силу и момент от всех зон
Vector3 turbulenceForce = turbulenceManager.GetTurbulenceForce(shipPosition, deltaTime);
Vector3 turbulenceTorque = turbulenceManager.GetTurbulenceTorque(shipPosition, deltaTime);

// Применяем силу (линейное движение)
shipRigidbody.AddForceAtPosition(turbulenceForce, centerOfMass, ForceMode.Force);

// Применяем момент (вращение) - основной эффект
shipRigidbody.AddTorque(turbulenceTorque, ForceMode.Force);
```

**Принципы:**
- **Perlin Noise** - создает плавную, естественную случайность (не резкие скачки)
- **Интенсивность зоны** - плавно затухает к краям (квадратичная функция)
- **Вращение > Движение** - `rotationBias = 0.8` означает, что 80% энергии идет на вращение

---

## 3. ГЕНЕРАЦИЯ: ОБЛАКА, ТУРБУЛЕНТНОСТЬ, ЗЕМЛЯ

### Принципы программирования:

#### A. **Процедурная генерация (Procedural Generation)**
Генерация контента **на лету** по алгоритму, а не из готовых данных.

#### B. **Чанковая система (Chunk System)**
Мир разбит на **чанки** (квадратные области), которые:
- Генерируются **по требованию** (когда корабль приближается)
- Удаляются, когда корабль уходит далеко
- Используют **детерминированные сиды** (одинаковые координаты = одинаковый результат)

#### C. **Кэширование (Caching)**
Результаты генерации сохраняются, чтобы не пересчитывать каждый раз.

---

### ГЕНЕРАЦИЯ ЗЕМЛИ (HillGenerator.cs)

**Принцип:** Процедурная генерация на основе **эллиптических холмов**

```csharp
// 1. Мир разбит на секторы (900м x 900м)
Vector2Int sectorCoord = GetSectorCoordinate(pos);

// 2. Для каждого сектора генерируются холмы
List<Hill> hills = GenerateHillsForSector(sectorCoord);

// 3. Каждый холм - эллипс с параметрами:
struct Hill {
    Vector2 position;    // Позиция центра
    float radiusX;       // Радиус по X
    float radiusZ;       // Радиус по Z
    float rotation;      // Поворот эллипса
    float height;        // Высота холма
}

// 4. Высота в точке = сумма вкладов всех холмов
float totalHeight = 0f;
foreach (Hill hill in hills) {
    float distToHill = GetEllipseDistance(pos, hill);
    if (distToHill < 1f) {
        float heightFalloff = (1f - distToHill²)²;  // Квадратичное затухание
        totalHeight += hill.height * heightFalloff;
    }
}
```

**Принципы:**
- **Детерминированный сид** - координаты → сид → одинаковый результат
- **Кэширование** - секторы сохраняются в `Dictionary<Vector2Int, List<Hill>>`
- **Эллипсы** - более естественная форма, чем круги
- **Квадратичное затухание** - плавное слияние холмов

---

### ГЕНЕРАЦИЯ ОБЛАКОВ (VolumetricCloudManager.cs)

**Принцип:** Чанковая система + процедурная генерация

```csharp
// 1. Мир разбит на чанки (500м x 500м)
Vector2Int chunkCoord = GetChunkCoordinate(shipPosition);

// 2. Генерируем облака в чанках вокруг корабля
for (int x = -loadRadius; x <= loadRadius; x++) {
    for (int z = -loadRadius; z <= loadRadius; z++) {
        Vector2Int chunk = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
        GenerateChunk(chunk);  // Генерируем облака в чанке
    }
}

// 3. Каждое облако генерируется с уникальным сидом
int seed = GetSeedForChunk(chunkCoord) + cloudIndex * 12345;
Random.InitState(seed);
Vector3 cloudPos = GetRandomPositionInChunk(...);
```

**Текстура облаков (CloudTextureGenerator.cs):**
```csharp
// Используем FBM (Fractal Brownian Motion) - многослойный Perlin Noise
for (int i = 0; i < 4; i++) {
    value += amplitude * Mathf.PerlinNoise(xCoord * frequency, yCoord * frequency);
    amplitude *= 0.5f;  // Каждый слой слабее
    frequency *= 2f;     // Каждый слой детальнее
}
```

**Принципы:**
- **FBM** - создает естественные, фрактальные формы
- **Чанки** - генерируются и удаляются динамически
- **Высота** - облака генерируются на разных высотах (30-200м над землей или во всей атмосфере)

---

### ГЕНЕРАЦИЯ ТУРБУЛЕНТНОСТИ (TurbulenceManager.cs)

**Принцип:** Аналогично облакам - чанковая система

```csharp
// 1. Генерируем зоны турбулентности в чанках
Vector2Int chunkCoord = GetChunkCoordinate(shipPosition);
GenerateChunk(chunkCoord);

// 2. Каждая зона - куб с параметрами:
TurbulenceZone zone = CreateTurbulenceZone(position, chunkCoord, zoneIndex, seed);
zone.SetZoneSize(randomSize);           // Случайный размер
zone.SetTurbulenceStrength(randomStrength);  // Случайная сила

// 3. Зоны генерируются на высотах 20-200м над землей
float groundHeight = HillGenerator.GetHeightAtPosition(worldPos);
float y = groundHeight + Random.Range(minHeightAboveGround, maxHeightAboveGround);
```

**Принципы:**
- **Детерминированность** - одинаковые координаты = одинаковые зоны
- **Чанки** - зоны удаляются, когда чанк выходит за пределы видимости
- **Perlin Noise** - внутри зоны используется для плавной турбулентности

---

## 4. ПОИСК ТОЧЕК ДЛЯ ПОСАДКИ (LandingRadar.cs)

### Логика поиска:

#### Шаг 1: **Создание сетки точек**
```csharp
// Создаем сетку точек вокруг корабля
int gridSize = Mathf.CeilToInt((scanRadius * 2f) / gridResolution);
for (int x = 0; x < gridSize; x++) {
    for (int z = 0; z < gridSize; z++) {
        Vector2 point = new Vector2(worldX, worldZ);
        // Сортируем по расстоянию (ближайшие первыми)
    }
}
```

#### Шаг 2: **Оценка каждой точки** (`EvaluateLandingSite`)

**A. Проверка ровности (CheckFlatness):**
```csharp
// Проверяем 8 точек по кругу вокруг центра
for (int i = 0; i < 8; i++) {
    Vector3 checkPos = center + new Vector3(
        Mathf.Cos(angle) * FLATNESS_CHECK_RADIUS,
        0f,
        Mathf.Sin(angle) * FLATNESS_CHECK_RADIUS
    );
    heights.Add(HillGenerator.GetHeightAtPosition(checkPos));
}

// Вычисляем стандартное отклонение
float variance = heights.Sum(h => (h - mean)²) / heights.Count;
float flatness = Mathf.Sqrt(variance);  // Чем меньше - тем ровнее
```

**B. Проверка наклона (CheckSlope):**
```csharp
// Проверяем наклон в 4 направлениях
foreach (Vector3 dir in {forward, back, left, right}) {
    Vector3 checkPos = center + dir * FLATNESS_CHECK_RADIUS;
    float heightDiff = Mathf.Abs(checkHeight - centerHeight);
    float angle = Mathf.Atan2(heightDiff, distance) * Mathf.Rad2Deg;
    maxSlope = Mathf.Max(maxSlope, angle);
}
```

**C. Проверка размера (CheckSiteSizeWithObstacles):**
```csharp
// Начинаем с минимального радиуса и расширяем
for (float radius = 15m; radius <= 90m; radius += 3m) {
    // Проверяем 16 точек по окружности
    for (int i = 0; i < 16; i++) {
        Vector3 checkPos = center + new Vector3(
            Mathf.Cos(angle) * radius,
            0f,
            Mathf.Sin(angle) * radius
        );
        
        // Проверяем ровность
        if (heightDiff <= MAX_FLATNESS_DEVIATION) {
            validPoints++;
        }
        
        // Проверяем препятствия (деревья, камни)
        Collider[] obstacles = Physics.OverlapCapsule(...);
        if (obstacleFound) {
            break;  // Не можем расширить дальше
        }
    }
    
    // Если 75% точек валидны - радиус пригоден
    if (validPoints >= requiredPoints) {
        maxRadius = radius;
    }
}
```

**D. Проверка препятствий:**
```csharp
// Используем Physics.OverlapCapsule для поиска объектов
Collider[] obstacles = Physics.OverlapCapsule(
    new Vector3(pos.x, groundHeight, pos.z),
    new Vector3(pos.x, groundHeight + 50m, pos.z),
    radius
);

// Фильтруем:
// - Исключаем корабль
// - Исключаем триггеры
// - Исключаем объекты под землей
// - Проверяем, что объект выше земли (деревья, камни)
```

#### Шаг 3: **Оценка пригодности**
```csharp
float suitabilityScore = CalculateSuitabilityScore(
    flatness,      // Чем меньше - тем лучше
    slopeAngle,    // Чем меньше - тем лучше
    siteSize,      // Чем больше - тем лучше
    obstacleDistance,  // Чем больше - тем лучше
    distanceFromShip   // Чем меньше - тем лучше
);
```

#### Шаг 4: **Группировка близких площадок**
```csharp
// Объединяем площадки, которые слишком близко друг к другу
foreach (var site in foundSites) {
    bool isTooClose = existingSites.Any(existing => 
        Vector3.Distance(site.position, existing.position) < 
        (site.size + existing.size) * 0.5f + MIN_DISTANCE_BETWEEN_SITES
    );
    if (!isTooClose) {
        result.Add(site);
    }
}
```

### Принципы программирования:

1. **Распределенная обработка** - точки обрабатываются по частям (30 точек/кадр), чтобы не зависать
2. **Приоритизация** - точки ближе к кораблю обрабатываются первыми
3. **Кэширование** - высоты земли кэшируются в `HillGenerator`
4. **Физические запросы** - `Physics.OverlapCapsule` для поиска препятствий
5. **Детерминированность** - одинаковые координаты = одинаковый результат

---

## ИТОГОВАЯ СХЕМА:

```
ВЕТЕР:
  Направление + Сила → Вектор силы → AddForceAtPosition → Корабль движется и крутится

ТУРБУЛЕНТНОСТЬ:
  Зоны (кубы) → Perlin Noise → Случайные силы/моменты → AddForce/AddTorque → Корабль трясет

ГЕНЕРАЦИЯ:
  Координаты → Сид → Случайные параметры → Объекты (холмы/облака/зоны) → Кэш

ПОИСК ПОСАДКИ:
  Сетка точек → Проверка (ровность/наклон/размер/препятствия) → Оценка → Группировка → Результат
```

---

## Ключевые алгоритмы:

1. **Perlin Noise** - для плавной случайности (облака, турбулентность)
2. **FBM (Fractal Brownian Motion)** - многослойный Perlin Noise (текстуры облаков)
3. **Чанковая система** - для бесконечного мира
4. **Кэширование** - для производительности
5. **Детерминированные сиды** - для воспроизводимости
6. **Физические запросы** - для обнаружения препятствий
7. **Распределенная обработка** - для плавности (не все за один кадр)
