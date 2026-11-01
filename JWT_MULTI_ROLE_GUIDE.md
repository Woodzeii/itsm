# JWT Token Claims - Многоролевые токены

## Описание

JWT-токены теперь содержат все роли пользователя в двух форматах:

1. **Классические Role Claims** — для работы с ASP.NET `[Authorize(Roles = "...")]` атрибутами
2. **JSON-массив ролей** в custom claim `"roles"` — для валидации на фронтенде на этапе сессии

## Структура Claims в JWT

```json
{
  "Id": "1",
  "Login": "admin",
  "Email": "admin@itsm.local",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["admin", "manager"],
  "roles": "[\"admin\", \"manager\"]",
  "jti": "550e8400-e29b-41d4-a716-446655440000",
  "iat": "1666666666",
  "iss": "itsm-api",
  "aud": "itsm-client",
  "exp": 1666674666
}
```

### Пояснение Claims:

| Claim | Тип | Описание |
|-------|-----|---------|
| `Id` | string | ID пользователя |
| `Login` | string | Логин пользователя |
| `Email` | string | Email пользователя |
| `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | array | Роли (стандартный ASP.NET формат) |
| `roles` | JSON string | Все роли как JSON-массив для фронтенда |
| `jti` | string | Unique JWT ID |
| `iat` | integer | Issued at timestamp |
| `iss` | string | Issuer (itsm-api) |
| `aud` | string | Audience (itsm-client) |
| `exp` | integer | Expiration timestamp |

## Использование на фронтенде

### Пример 1: Декодирование токена и получение ролей

```typescript
import { jwtDecode } from 'jwt-decode';

interface JwtPayload {
  Id: string;
  Login: string;
  Email: string;
  roles: string; // JSON-массив ролей как строка
  iat: number;
  exp: number;
}

function getUserRoles(token: string): string[] {
  const decoded = jwtDecode<JwtPayload>(token);
  
  // Парсим JSON-массив из claim "roles"
  const roles = JSON.parse(decoded.roles);
  return roles; // ["admin", "manager", ...]
}
```

### Пример 2: Валидация доступа на основе ролей (React)

```typescript
interface AuthContext {
  user: {
    id: string;
    login: string;
    email: string;
    roles: string[];
  } | null;
  token: string | null;
}

function hasRole(roles: string[], requiredRole: string): boolean {
  return roles.includes(requiredRole);
}

function hasAnyRole(roles: string[], requiredRoles: string[]): boolean {
  return requiredRoles.some(role => roles.includes(role));
}

function hasAllRoles(roles: string[], requiredRoles: string[]): boolean {
  return requiredRoles.every(role => roles.includes(role));
}

// Использование в компонентах
function AdminPanel() {
  const { user } = useAuth();
  
  if (!user || !hasRole(user.roles, 'admin')) {
    return <AccessDenied />;
  }
  
  return <AdminContent />;
}
```

### Пример 3: Route Guard (Angular)

```typescript
import { Injectable } from '@angular/core';
import { Router, CanActivate, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable()
export class RoleGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const requiredRoles = route.data['roles'] as string[];
    const userRoles = this.authService.getUserRoles();

    if (!requiredRoles || requiredRoles.length === 0) {
      return true;
    }

    const hasRequiredRole = requiredRoles.some(role => 
      userRoles.includes(role)
    );

    if (!hasRequiredRole) {
      this.router.navigate(['/access-denied']);
      return false;
    }

    return true;
  }
}

// В конфигурации маршрутов
const routes = [
  {
    path: 'admin',
    component: AdminPanelComponent,
    canActivate: [RoleGuard],
    data: { roles: ['admin', 'tenant_admin'] }
  }
];
```

### Пример 4: Сохранение ролей в локальное хранилище

```typescript
function parseAndStoreToken(token: string): void {
  const decoded = jwtDecode<JwtPayload>(token);
  
  const user = {
    id: decoded.Id,
    login: decoded.Login,
    email: decoded.Email,
    roles: JSON.parse(decoded.roles),
    expiresAt: new Date(decoded.exp * 1000)
  };

  // Сохраняем в localStorage
  sessionStorage.setItem('currentUser', JSON.stringify(user));
  sessionStorage.setItem('token', token);
}

function getCurrentUserRoles(): string[] {
  const userJson = sessionStorage.getItem('currentUser');
  if (!userJson) return [];
  
  const user = JSON.parse(userJson);
  return user.roles || [];
}
```

## Backend - Отправка токена

Когда пользователь успешно прошел 2FA, сервер отправляет:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 3600
}
```

Фронтенд должен:
1. Сохранить `accessToken` в secure storage
2. Декодировать токен и извлечь `roles` claim
3. Сохранить роли в контекст приложения
4. Использовать роли для контроля доступа к функциям

## Безопасность

⚠️ **Важно:**
- JWT-токен содержит Claims в открытом виде (base64), не криптографически подписан
- Всегда проверяйте роли на **бэкенде** для критичных операций
- Клиентская валидация ролей — только для улучшения UX (скрытие недоступных кнопок и т.п.)
- Никогда не доверяйте только фронтенд-валидации для защиты sensitive данных

## Структура Roles в разных ролях

После 2FA пользователь получает токен с его ролями:

### Администратор
```json
{
  "roles": "[\"admin\", \"tenant_admin\"]"
}
```

### Инженер поддержки
```json
{
  "roles": "[\"agent\"]"
}
```

### Руководитель
```json
{
  "roles": "[\"manager\"]"
}
```

### Пользователь портала
```json
{
  "roles": "[\"portal_user\"]"
}
```

## Тестирование

Используйте сайт [jwt.io](https://jwt.io) для декодирования и анализа токенов:
1. Скопируйте accessToken из ответа `/api/auth/verify-2fa`
2. Вставьте на jwt.io
3. Смотрите Claims в разделе "Decoded"

## Выбор активной роли после 2FA (сессия)

После `/api/auth/verify-2fa` фронтенд запрашивает сессию у бэкенда — роли и активную роль
определяет сервер, а не клиент.

### `GET /api/auth/session`

Заголовок: `Authorization: Bearer <accessToken>`

```json
{
  "userId": 7,
  "login": "m.multi",
  "email": "m.multi@itsm.local",
  "roles": ["admin", "agent"],
  "activeRole": null,
  "requiresRoleSelection": true
}
```

- `requiresRoleSelection = true` — у пользователя несколько ролей и активная ещё не выбрана;
  фронтенд открывает промежуточный экран `/select-role`.
- Для пользователя с одной ролью бэкенд сразу возвращает `activeRole`, и экран не показывается.

### `POST /api/auth/session/role`

```json
{ "role": "agent" }
```

Сервер сверяет роль с назначенными пользователю в БД и выпускает новый accessToken:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "activeRole": "agent",
  "roles": ["admin", "agent"]
}
```

В новом токене:

- `roles` — все роли пользователя (JSON-массив, для UI);
- `activeRole` — выбранная активная роль;
- Role-claim — **только выбранная роль**, поэтому `[Authorize(Roles = "...")]` на бэкенде
  и Router Guards на фронтенде работают согласованно.

### Смена активной роли без повторного входа

`POST /api/auth/session/role` можно вызывать повторно с действующим токеном — сервер снова
проверит роль по БД и выпустит токен под неё. В интерфейсе у пользователя с несколькими
ролями рядом с кнопкой выхода отображается **«Сменить роль»**, которая открывает
`/select-role?switch=1`. В этом режиме экран не редиректит сразу в рабочее пространство,
подсвечивает текущую роль и позволяет выбрать другую (или отменить смену).

> Мультиролевой токен, выданный сразу после 2FA, не содержит Role-claims: до выбора роли
> role-protected API недоступны. Это защищает от обхода экрана выбора прямым запросом к API.

