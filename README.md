# Doject - Data-orinted Inject `v1.0.0`
Dev Tools for Unity DOTS

---
Возможности:
- Инжекция лукапов внутрь `IJobEntity`
- Автоматическая инициализация и обновления кеша для `IJobEntity`

## Сравнение
### Изначальный код (65 строк)
```csharp
using Movement;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
public partial struct AtrTestSystem : ISystem
{
    private ComponentLookup<AimState> _aimLookup;
    private ComponentLookup<MoveDirection> _directionLookup;
    private ComponentLookup<ArmorBoost> _armorBoostLookup;
    private BufferLookup<Buffs.BuffInstance> _buffsLookup;

    public void OnCreate(ref SystemState state)
    {
        _aimLookup = state.GetComponentLookup<AimState>(isReadOnly: true);
        _directionLookup = state.GetComponentLookup<MoveDirection>(isReadOnly: true);
        _armorBoostLookup = state.GetComponentLookup<ArmorBoost>();
        _buffsLookup = state.GetBufferLookup<Buffs.BuffInstance>(isReadOnly: true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _aimLookup.Update(ref state);
        _directionLookup.Update(ref state);
        _armorBoostLookup.Update(ref state);
        _buffsLookup.Update(ref state);

        var job = new AtrTestJob
        {
            AimLookup = _aimLookup,
            DirectionLookup = _directionLookup,
            ArmorBoostLookup = _armorBoostLookup,
            BuffsLookup = _buffsLookup
        };

        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(PlayerTag))]
    public partial struct AtrTestJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<AimState> AimLookup;
        [ReadOnly] public ComponentLookup<MoveDirection> DirectionLookup;
        public ComponentLookup<ArmorBoost> ArmorBoostLookup;
        [ReadOnly] public BufferLookup<Buffs.BuffInstance> BuffsLookup;

        private void Execute(Entity entity)
        {
            if (AimLookup.TryGetComponent(entity, out var aim) && aim.IsAiming)
                Debug.Log($"Aim direction: {aim.Direction}. Distance: {aim.Distance}");

            if (ArmorBoostLookup.TryGetComponent(entity, out var armorBoost))
                Debug.Log($"Armor boost: {armorBoost.Value}");

            if (DirectionLookup.TryGetComponent(entity, out var direction))
                Debug.Log($"Speed: {direction.Speed}. Direction:{direction.Direction}");

            if (BuffsLookup.TryGetBuffer(entity, out var buffs))
                Debug.Log($"Buffs count: {buffs.Length}");
        }
    }
}
```

### Код с кешом и атрибутом `[AutoInject]` (50 строк)
```csharp
using Doject.Attributes;
using Movement;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
public partial struct AtrTestSystem : ISystem
{
    private AtrTestJob.Cache _atrTestJobCache;

    public void OnCreate(ref SystemState state)
    {
        _atrTestJobCache.Init(ref state);
    }

    public void OnUpdate(ref SystemState state)
    {
        _atrTestJobCache.Update(ref state);

        new AtrTestJob(ref _atrTestJobCache).Schedule();
    }

    [BurstCompile]
    [AutoInject]
    [WithAll(typeof(PlayerTag))]
    public partial struct AtrTestJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<AimState> AimLookup;
        [ReadOnly] public ComponentLookup<MoveDirection> DirectionLookup;
        public ComponentLookup<ArmorBoost> ArmorBoostLookup;
        [ReadOnly] public BufferLookup<Buffs.BuffInstance> BuffsLookup;

        private void Execute(Entity entity)
        {
            if (AimLookup.TryGetComponent(entity, out var aim) && aim.IsAiming)
                Debug.Log($"Aim direction: {aim.Direction}. Distance: {aim.Distance}");

            if (ArmorBoostLookup.TryGetComponent(entity, out var armorBoost))
                Debug.Log($"Armor boost: {armorBoost.Value}");

            if (DirectionLookup.TryGetComponent(entity, out var direction))
                Debug.Log($"Speed: {direction.Speed}. Direction:{direction.Direction}");

            if (BuffsLookup.TryGetBuffer(entity, out var buffs))
                Debug.Log($"Buffs count: {buffs.Length}");
        }
    }
}
```

### Код с `[AutoInject]` и `[AutoInjectSystem]` (41 строка)
```csharp
using Doject.Attributes;
using Movement;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
[AutoInjectSystem]
public partial struct AtrTestSystem : ISystem
{
    partial void OnSystemUpdate(ref SystemState state)
    {
        new AtrTestJob(ref _atrTestJobCache).Schedule();
    }

    [BurstCompile, AutoInject]
    [WithAll(typeof(PlayerTag))]
    public partial struct AtrTestJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<AimState> AimLookup;
        [ReadOnly] public ComponentLookup<MoveDirection> DirectionLookup;
        public ComponentLookup<ArmorBoost> ArmorBoostLookup;
        [ReadOnly] public BufferLookup<Buffs.BuffInstance> BuffsLookup;

        private void Execute(Entity entity)
        {
            if (AimLookup.TryGetComponent(entity, out var aim) && aim.IsAiming)
                Debug.Log($"Aim direction: {aim.Direction}. Distance: {aim.Distance}");

            if (ArmorBoostLookup.TryGetComponent(entity, out var armorBoost))
                Debug.Log($"Armor boost: {armorBoost.Value}");
            
            if (DirectionLookup.TryGetComponent(entity, out var direction))
                Debug.Log($"Speed: {direction.Speed}. Direction:{direction.Direction}");

            if (BuffsLookup.TryGetBuffer(entity, out var buffs))
                Debug.Log($"Buffs count: {buffs.Length}");
        }
    }
}
```

## Преимущества
- Используется автогенерация кода на основе атрибутов
- Совместимо полностью с `[BurstCompile]`
- Не нужно следить за передачей лукапов, вручную кешировать и обновлять
- `[IgnoreInjectAttribute]` - атрибут для исключения лукапа из инжекта
- Меньше технического кода

## Ограничения атрибута `[AutoInjectSystem]`
- Необходимо использовать именно `partial OnSystemCreate` и `partial OnSystemUpdate` (`OnDestroy` по-прежнему можно использовать)

## Принцип работы
1. **Как работает `[AutoInject]` (на уровне Джобы)**

> Генератор ищет структуру джобы с этим атрибутом и создает для неё вложенную структуру Cache
- Собирает поля: Находит все `ComponentLookup<T>` и `BufferLookup<T>` внутри джобы (пропуская помеченные `[IgnoreInject]`).
- `Init(ref state)`: Вызывает `state.GetComponentLookup<T>(isReadOnly)` один раз при старте.
- `Update(ref state)`: Обновляет состояние лукапов перед выполнением джобы, вызывая lookup.
- `Update(ref state).Apply(...)`: Копирует актуальные лукапы из Cache прямо в поля самой джобы.
3. **Как работает `[AutoInjectSystem]` (на уровне Системы)**
> Генератор находит структуру системы с этим атрибутом и под капотом реализует явный интерфейс ISystem
- Генерирует поля кэшей: Для каждой вложенной джобы с атрибутом `[AutoInject]` создает закрытое поле кэша (например, `_atrTestJobCache`).
- `ISystem.OnCreate`: Автоматически вызывает `.Init(ref state)` для всех кэшей джоб, а затем передает управление в `OnSystemCreate`.
- `ISystem.OnUpdate`: Автоматически вызывает `.Update(ref state)` для всех кэшей джоб, после чего передает управление в `OnSystemUpdate`.

### Итоговый поток выполнения в рантайме
1. **Unity** вызывает `ISystem.OnUpdate` $\rightarrow$
2. Фреймворк обновляет кэши: Вызывает `Update(ref state)` для всех `ComponentLookup` $\rightarrow$
3. Вызывается `OnSystemUpdate`: где можно создать джобу и передать в нее кэш new `MyJob(ref _myJobCache)` $\rightarrow$
4. Джоба получает свежие лукапы и безопасно планируется через `.Schedule()` или `.SheduleParallel()` с поддержкой очереди `state.Dependency` (`JobHandle`).

## Установка и использование в Unity
> Source Generator поставляется в виде обычного C# Roslyn-генератора (.dll), который подключается к проекту Unity через Assembly Definition или директорию Plugins.
### Сборка проекта генератора
> Можете просто скачать бинарники с _Releases_ на _GitHub_
1. Откройте проект генератора в вашей IDE (Rider / Visual Studio).
2. Убедитесь, что таргет-фреймворк проекта указан как `netstandard2.0` (требование Unity к Roslyn Analyzer). 
3. Соберите проект в режиме _Release_:
```bash
dotnet build -c Release
```
4. Заберите скомпилированный файл `Doject.dll` из папки `Doject\bin\Release\netstandard2.0` и `Doject.Attributes.dll' из папки `Doject.Attributes/bin/Release/netstandard2.0`

### Подключение в Unity Editor
1. Перенесите `Doject.dll` (и файл атрибутов `Doject.Attributes.dll`) в любую папку внутри проекта Unity (например, `Assets/Plugins/`).
2. Выберите `Doject.dll` в инспекторе Unity:
   - В секции Select platforms for plugin снимите галочки со всех платформ (Any CPU, Standalone, Editor и т.д.).
   - Перейдите в категорию Asset Labels (иконка ярлыка внизу Inspector) или в настройки Plugin Inspector:
   - Добавьте плагину метку: RoslynAnalyzer.
3. Нажмите Apply.

## Таск-лист
- [x] Инекция лукапов в `IJobEntity`
- [x] Авто-обновление кеша с совместной генерацией лукапов для `IJobEntity` в системах
- [ ] Упрощенный доступ к `EntityCommandBuffer` и авто ожидание в `OnCreate`
- [ ] Использование обычных или _partial_ `OnCreate` и `OnUpdate` при `[AutoInjectSystem]`
- [ ] Поддержка переименования `IJobEntity` (автоматическое обновление названия кеша)
- [ ] Поддержка `[AutoInjectSystem]` с `IJobEntity` которые находятся все системы, но используются в системе _(In progress)_

---
### Что еще планируется в качестве дополнений (отдельные DLL)
- [ ] Интрументы для работы с физическими событиями _(In progress)_
