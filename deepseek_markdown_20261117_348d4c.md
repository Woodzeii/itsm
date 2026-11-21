# Спецификация бизнес-логики SLA и тест-кейсы

**Тип:** Аналитика / QA  
**Приоритет:** Highest  
**Цель:** Эталонный документ для разработки «Ядра SLA»: алгоритм расчёта дедлайнов, пауз и рабочих часов, edge-cases, тест-кейсы и тестовые данные.

---

## 1. Математический алгоритм расчёта SLA

### 1.1. Термины и допущения

- **SLA-таймер** считается в **рабочих минутах** тенанта.
- **Рабочий календарь** задаёт:
  - таймзону тенанта;
  - рабочие дни недели;
  - рабочие интервалы внутри дня, например `09:00–13:00`, `14:00–18:00`;
  - праздники;
  - исключения на конкретную дату: выходной/рабочий/сокращённый день.
- Приоритет календаря: **исключение на дату > праздник > обычный день недели**.
- Интервалы считаются полуоткрытыми: `[начало, конец)`. Время `18:00` уже не рабочее.
- Пауза SLA — статус, в котором таймер замораживается. Для паузы копится **только рабочее время** внутри интервала паузы. Нерабочее время паузы не даёт дополнительного сдвига, потому что SLA в это время и так не шёл.
- Если заявка уже просрочена, последующая пауза **не отменяет факт breach**. `breach_at` фиксируется.
- Календарь лучше **снапшотить** версией на момент назначения SLA. Изменение календаря задним числом не должно молча пересчитывать старые SLA.

> Важно: пример из ТЗ «пятница 17:00, SLA 2 часа → вторник 10:00» корректен только если **понедельник — нерабочий/праздник**. Иначе при календаре Пн–Пт 09:00–18:00 дедлайн будет **понедельник 10:00**.

---

### 1.2. Функция `add_working_minutes(start, minutes, calendar)`

Добавляет `minutes` рабочих минут к моменту `start`.

```text
add_working_minutes(start, minutes, calendar):
    if minutes <= 0:
        return start

    cur = next_working_instant(start, calendar)
    remaining = minutes

    while remaining > 0:
        interval = working_interval_containing(cur, calendar)
        available = minutes_between(cur, interval.end)

        if remaining <= available:
            return cur + remaining minutes

        remaining = remaining - available
        cur = next_working_instant(interval.end, calendar)

    return cur
```

`next_working_instant(t)`:
- если `t` внутри рабочего интервала — вернуть `t`;
- если `t` ровно на конце интервала или вне рабочего времени — вернуть начало следующего рабочего интервала.

---

### 1.3. Функция `working_minutes_between(a, b, calendar)`

Считает, сколько рабочих минут попало в интервал `[a, b)`.

```text
working_minutes_between(a, b, calendar):
    if b <= a:
        return 0

    total = 0
    for each date in dates_from(a - 1 day) to (b + 1 day):
        for each interval in working_intervals(date, calendar):
            total += overlap_minutes([a, b), interval)

    return total
```

---

### 1.4. Расчёт дедлайна с паузами

Вход:
- `created_at` — время создания заявки;
- `duration_min` — SLA в рабочих минутах;
- `calendar` — календарь тенанта;
- события статусов, отсортированные по времени.

```text
calculate_sla_deadline(created_at, duration_min, calendar, status_events):
    base_deadline = add_working_minutes(created_at, duration_min, calendar)
    deadline = base_deadline
    breach_at = null
    is_paused = false
    pause_start = null

    for event in status_events sorted by event.at:
        # 1. Проверка breach до обработки события
        if not is_paused and breach_at is null and event.at >= deadline:
            breach_at = deadline

        # 2. Переход в паузу
        if event.status in PAUSE_STATUSES:
            if not is_paused:
                is_paused = true
                pause_start = event.at

        # 3. Возврат из паузы в активный статус
        elif event.status in ACTIVE_STATUSES:
            if is_paused:
                delta = working_minutes_between(pause_start, event.at, calendar)

                if breach_at is null and pause_start < deadline:
                    deadline = add_working_minutes(deadline, delta, calendar)

                is_paused = false
                pause_start = null

        # 4. Терминальный статус — SLA больше не тикает
        elif event.status in STOP_STATUSES:
            if is_paused:
                delta = working_minutes_between(pause_start, event.at, calendar)

                if breach_at is null and pause_start < deadline:
                    deadline = add_working_minutes(deadline, delta, calendar)

                is_paused = false
                pause_start = null
            break

    # 5. Финальная проверка на текущий момент / момент закрытия
    if not is_paused and breach_at is null and now >= deadline:
        breach_at = deadline

    return {
        base_deadline: base_deadline,
        deadline: deadline,
        breach_at: breach_at,
        is_paused: is_paused
    }
```

Формула сдвига на паузу:

```text
deadline_new = add_working_minutes(deadline_old, Δpause_work)
Δpause_work = working_minutes_between(pause_start, pause_end, calendar)
```

Сдвиг применяется, только если пауза началась **до** текущего дедлайна и SLA ещё не был просрочен.

---

## 2. Матрица исключений / Edge Cases

| № | Ситуация | Правило | Ожидание |
|---|---|---|---|
| 1 | Заявка создана за 5 минут до конца рабочего дня | Считаем 5 рабочих минут, остаток переносим на следующий рабочий интервал | Не «округляем» до конца дня |
| 2 | Заявка создана ровно в `18:00` | `18:00` уже не рабочее время | Старт с начала следующего рабочего дня |
| 3 | Заявка создана ночью / в выходной | `next_working_instant` переносит на начало следующего рабочего дня | SLA не тикает до рабочего времени |
| 4 | У тенанта праздник, но инженер работает | Ядро SLA использует календарь тенанта. Чтобы день считался рабочим, нужна дата-исключение | Без исключения SLA не тикает |
| 5 | Праздник в календаре, но есть исключение «рабочий день» | Исключение важнее праздника | День считается рабочим |
| 6 | Заявка висит в паузе 3 дня | Копим только рабочие минуты внутри паузы | Выходные/праздники не добавляют сдвиг |
| 7 | Пауза началась до дедлайна, а закончилась после старого дедлайна | Дедлайн сдвигается на рабочие минуты паузы; breach во время паузы не фиксируется | После возврата проверяем новый дедлайн |
| 8 | Пауза началась уже после breach | Пауза не отменяет просрочку | `breach_at` остаётся прежним |
| 9 | Пауза целиком вне рабочих часов | `Δpause_work = 0` | Дедлайн не меняется |
| 10 | Несколько пауз подряд | Сдвиги применяются последовательно / эквивалентно сумме рабочих минут пауз | Главное — не считать пересечения дважды |
| 11 | Сокращённый день, например пятница `09:00–15:00` | Используем интервалы из исключения | После `15:00` перенос на следующий рабочий день |
| 12 | Обед / несколько интервалов в дне | Обед не считается рабочим временем | `12:00–14:00` не расходует SLA |
| 13 | Переход на летнее/зимнее время | Работаем с timezone-aware datetime; интервалы заданы в локальном времени тенанта | Минуты считаем по абсолютным instant-ам |
| 14 | SLA duration `0` или отрицательный | Некорректная настройка | Валидация: `duration_min > 0`, дедлайн не создаётся |
| 15 | Заявка закрыта, пока была в паузе | SLA останавливается; breach не фиксируется, если не был раньше | Для аудита можно пересчитать дедлайн на момент закрытия |
| 16 | Календарь изменили после создания заявки | Старые SLA не пересчитываются автоматически | Нужна явная миграция/пересчёт |

---

## 3. Набор тест-кейсов QA

Базовый календарь `CAL_BASE`: Пн–Пт `09:00–18:00`, таймзона `Europe/Moscow`, без праздников. SLA — 120 минут, если не указано иное.

| ID | Given — дано | When — действие | Then — ожидание |
|---|---|---|---|
| TC-01 | `CAL_BASE`, SLA 2 ч | Заявка создана Пн 10:00 | Дедлайн Пн 12:00 |
| TC-02 | `CAL_BASE`, SLA 2 ч | Создана Пн 17:00 | Дедлайн Вт 10:00 |
| TC-03 | `CAL_BASE`, SLA 2 ч | Создана Пн 17:55 | Дедлайн Вт 10:55 |
| TC-04 | `CAL_BASE`, SLA 2 ч | Создана Пн ровно в 18:00 | Дедлайн Вт 11:00 |
| TC-05 | `CAL_BASE`, SLA 2 ч | Создана Сб 12:00 | Дедлайн Пн 11:00 |
| TC-06 | `CAL_BASE` + праздник в Пн, SLA 2 ч | Создана Пт 17:00 | Дедлайн Вт 10:00 |
| TC-07 | `CAL_BASE`, SLA 8 ч | Создана Пн 09:00; пауза Пн 10:00–12:00 | Дедлайн Вт 10:00 |
| TC-08 | `CAL_BASE`, SLA 8 ч | Создана Пт 09:00; пауза Пт 16:00 – Пн 10:00 | Дедлайн Пн 11:00 |
| TC-09 | `CAL_BASE`, SLA 32 ч | Создана Пн 09:00; пауза Вт 09:00 – Пт 09:00 | Дедлайн Вт 14:00 |
| TC-10 | `CAL_BASE`, SLA 4 ч | Создана Пт 17:00; пауза Пт 18:00 – Пн 09:00 | Дедлайн Пн 12:00, пауза не сдвигает |
| TC-11 | Календарь: Пн праздник, но есть исключение «Пн 09:00–18:00 рабочий», SLA 2 ч | Создана Пт 17:00 | Дедлайн Пн 10:00 |
| TC-12 | Календарь: Пт сокращённый `09:00–15:00`, SLA 2 ч | Создана Пт 14:00 | Дедлайн Пн 10:00 |
| TC-13 | Календарь с обедом `09:00–13:00`, `14:00–18:00`, SLA 2 ч | Создана Пн 12:00 | Дедлайн Пн 15:00 |
| TC-14 | SLA duration = 0 или -1 | Создание заявки | Ошибка валидации, дедлайн не создан |
| TC-15 | `CAL_BASE`, SLA 2 ч | Создана Пн 09:00; пауза Пн 12:00–14:00 | `breach_at = Пн 11:00`; пауза не отменяет просрочку |

---

## 4. Тестовые данные / сидеры

### 4.1. JSON: календари и SLA-политики

```json
{
  "timezone_default": "Europe/Moscow",
  "calendars": [
    {
      "id": "cal_base",
      "version": 1,
      "timezone": "Europe/Moscow",
      "weekly": {
        "mon": [["09:00", "18:00"]],
        "tue": [["09:00", "18:00"]],
        "wed": [["09:00", "18:00"]],
        "thu": [["09:00", "18:00"]],
        "fri": [["09:00", "18:00"]],
        "sat": [],
        "sun": []
      },
      "holidays": [],
      "exceptions": []
    },
    {
      "id": "cal_holiday_mon",
      "version": 1,
      "timezone": "Europe/Moscow",
      "weekly": {
        "mon": [["09:00", "18:00"]],
        "tue": [["09:00", "18:00"]],
        "wed": [["09:00", "18:00"]],
        "thu": [["09:00", "18:00"]],
        "fri": [["09:00", "18:00"]],
        "sat": [],
        "sun": []
      },
      "holidays": ["2025-01-13"],
      "exceptions": []
    },
    {
      "id": "cal_holiday_exception",
      "version": 1,
      "timezone": "Europe/Moscow",
      "weekly": {
        "mon": [["09:00", "18:00"]],
        "tue": [["09:00", "18:00"]],
        "wed": [["09:00", "18:00"]],
        "thu": [["09:00", "18:00"]],
        "fri": [["09:00", "18:00"]],
        "sat": [],
        "sun": []
      },
      "holidays": ["2025-01-13"],
      "exceptions": [
        {
          "date": "2025-01-13",
          "type": "working",
          "intervals": [["09:00", "18:00"]],
          "reason": "Инженер работает в праздник"
        }
      ]
    },
    {
      "id": "cal_lunch",
      "version": 1,
      "timezone": "Europe/Moscow",
      "weekly": {
        "mon": [["09:00", "13:00"], ["14:00", "18:00"]],
        "tue": [["09:00", "13:00"], ["14:00", "18:00"]],
        "wed": [["09:00", "13:00"], ["14:00", "18:00"]],
        "thu": [["09:00", "13:00"], ["14:00", "18:00"]],
        "fri": [["09:00", "13:00"], ["14:00", "18:00"]],
        "sat": [],
        "sun": []
      },
      "holidays": [],
      "exceptions": []
    },
    {
      "id": "cal_short_fri",
      "version": 1,
      "timezone": "Europe/Moscow",
      "weekly": {
        "mon": [["09:00", "18:00"]],
        "tue": [["09:00", "18:00"]],
        "wed": [["09:00", "18:00"]],
        "thu": [["09:00", "18:00"]],
        "fri": [["09:00", "15:00"]],
        "sat": [],
        "sun": []
      },
      "holidays": [],
      "exceptions": []
    }
  ],
  "sla_policies": [
    {
      "id": "sla_2h_base",
      "name": "SLA 2 часа, базовый календарь",
      "duration_minutes": 120,
      "calendar_id": "cal_base",
      "pause_statuses": ["CHECK"],
      "active_statuses": ["NEW", "IN_PROGRESS"],
      "stop_statuses": ["RESOLVED", "CLOSED"]
    },
    {
      "id": "sla_8h_base",
      "name": "SLA 8 часов, базовый календарь",
      "duration_minutes": 480,
      "calendar_id": "cal_base",
      "pause_statuses": ["CHECK"],
      "active_statuses": ["NEW", "IN_PROGRESS"],
      "stop_statuses": ["RESOLVED", "CLOSED"]
    },
    {
      "id": "sla_32h_base",
      "name": "SLA 32 часа, базовый календарь",
      "duration_minutes": 1920,
      "calendar_id": "cal_base",
      "pause_statuses": ["CHECK"],
      "active_statuses": ["NEW", "IN_PROGRESS"],
      "stop_statuses": ["RESOLVED", "CLOSED"]
    },
    {
      "id": "sla_4h_base",
      "name": "SLA 4 часа, базовый календарь",
      "duration_minutes": 240,
      "calendar_id": "cal_base",
      "pause_statuses": ["CHECK"],
      "active_statuses": ["NEW", "IN_PROGRESS"],
      "stop_statuses": ["RESOLVED", "CLOSED"]
    }
  ]
}
```

### 4.2. CSV: праздники и исключения

```csv
calendar_id,date,type,intervals,reason
cal_holiday_mon,2025-01-13,holiday,,"Праздник тенанта"
cal_holiday_exception,2025-01-13,working,"09:00-18:00","Инженер работает в праздник"
```

### 4.3. CSV: SLA-политики для быстрой загрузки

```csv
id,name,duration_minutes,calendar_id,pause_statuses,active_statuses,stop_statuses
sla_2h_base,SLA 2 часа,120,cal_base,CHECK,"NEW|IN_PROGRESS","RESOLVED|CLOSED"
sla_8h_base,SLA 8 часов,480,cal_base,CHECK,"NEW|IN_PROGRESS","RESOLVED|CLOSED"
sla_32h_base,SLA 32 часа,1920,cal_base,CHECK,"NEW|IN_PROGRESS","RESOLVED|CLOSED"
sla_4h_base,SLA 4 часа,240,cal_base,CHECK,"NEW|IN_PROGRESS","RESOLVED|CLOSED"
```

---

## 5. Что согласовано с бэкендером

1. Пауза считается в **рабочих минутах**.
2. Если инженер работает в праздник, включаем это **датой-исключением** в календаре, а не отдельным флагом «инженер работает».
3. Календарь снапшотится версией: старые SLA не пересчитываются при изменении календаря.
4. Пауза после уже наступившего breach **не отменяет** просрочку.
5. Поддерживаем несколько SLA-таргетов независимо: реакция, решение и т.п. — каждый считается своим вызовом `calculate_sla_deadline`.
6. Нужна валидация: `duration_min > 0`, статусы пауз/активные/терминальные не пересекаются.
7. Для DST используем timezone-aware даты, интервалы заданы в локальной таймзоне тенанта.