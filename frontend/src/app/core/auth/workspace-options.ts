import type { UserRole } from './auth.service';

/** Вариант рабочего пространства, который показывается на экране выбора роли. */
export interface WorkspaceOption {
    role: UserRole;
    title: string;
    description: string;
    icon: string;
}

/** Порядок отображения: сначала инженерные интерфейсы, затем админские. */
const ROLE_ORDER: readonly UserRole[] = [
    'agent',
    'manager',
    'admin',
    'tenant_admin',
    'portal_user',
];

const ROLE_CATALOG: Record<UserRole, Omit<WorkspaceOption, 'role'>> = {
    agent: {
        title: 'Интерфейс инженера',
        description: 'Очередь заявок, активы и эскалации Service Desk.',
        icon: '🛠',
    },
    manager: {
        title: 'Интерфейс руководителя',
        description: 'Заявки команды, отчёты и контроль SLA.',
        icon: '📊',
    },
    admin: {
        title: 'Админка',
        description: 'Пользователи, настройки Service Desk, формы и правила эскалации.',
        icon: '⚙',
    },
    tenant_admin: {
        title: 'Админка организации',
        description: 'Управление тенантами и параметрами организации.',
        icon: '🏢',
    },
    portal_user: {
        title: 'Портал самообслуживания',
        description: 'Свои заявки и обращения в поддержку.',
        icon: '🎫',
    },
};

/** Преобразует коды ролей с бэкенда в варианты выбора без дублей. */
export function workspaceOptionsFor(roles: readonly string[]): WorkspaceOption[] {
    const available = new Set<UserRole>();
    for (const role of roles) {
        if (isUserRole(role)) {
            available.add(role);
        }
    }

    return ROLE_ORDER
        .filter(role => available.has(role))
        .map(role => ({ role, ...ROLE_CATALOG[role] }));
}

export function isUserRole(value: string): value is UserRole {
    return Object.prototype.hasOwnProperty.call(ROLE_CATALOG, value);
}
