/**
 * JWT Token Role Management Utility
 * 
 * Frontend utilities for decoding JWT tokens and working with multi-role claims.
 * Support for React, Angular, Vue and vanilla JavaScript.
 * 
 * Installation:
 * npm install jwt-decode
 */

import { jwtDecode } from 'jwt-decode';

/**
 * JWT Payload Interface
 * Defines the structure of decoded JWT token
 */
export interface JwtPayload {
  Id: string;
  Login: string;
  Email: string;
  roles: string; // JSON array of roles as string
  iat: number;
  exp: number;
  iss: string;
  aud: string;
}

/**
 * User Info extracted from JWT
 */
export interface UserInfo {
  id: string;
  login: string;
  email: string;
  roles: string[];
  expiresAt: Date;
}

/**
 * Decode JWT token and extract user info with roles
 * 
 * @param token JWT access token
 * @returns User info with decoded roles
 * @throws Error if token is invalid or missing roles claim
 */
export function decodeToken(token: string): UserInfo {
  try {
    const decoded = jwtDecode<JwtPayload>(token);

    if (!decoded.roles) {
      throw new Error('Token missing "roles" claim');
    }

    const roles = JSON.parse(decoded.roles);
    if (!Array.isArray(roles)) {
      throw new Error('Invalid roles format in token');
    }

    return {
      id: decoded.Id,
      login: decoded.Login,
      email: decoded.Email,
      roles,
      expiresAt: new Date(decoded.exp * 1000)
    };
  } catch (error) {
    if (error instanceof Error) {
      throw new Error(`Failed to decode token: ${error.message}`);
    }
    throw error;
  }
}

/**
 * Check if user has a specific role
 * 
 * @param roles Array of user roles
 * @param requiredRole Role to check
 * @returns true if user has the role
 */
export function hasRole(roles: string[], requiredRole: string): boolean {
  return roles.includes(requiredRole);
}

/**
 * Check if user has any of the specified roles
 * 
 * @param roles Array of user roles
 * @param requiredRoles Roles to check (OR logic)
 * @returns true if user has any of the roles
 */
export function hasAnyRole(roles: string[], requiredRoles: string[]): boolean {
  return requiredRoles.some(role => roles.includes(role));
}

/**
 * Check if user has all of the specified roles
 * 
 * @param roles Array of user roles
 * @param requiredRoles Roles to check (AND logic)
 * @returns true if user has all the roles
 */
export function hasAllRoles(roles: string[], requiredRoles: string[]): boolean {
  return requiredRoles.every(role => roles.includes(role));
}

/**
 * Check if token is expired
 * 
 * @param token JWT token or UserInfo
 * @returns true if token is expired
 */
export function isTokenExpired(token: string | UserInfo): boolean {
  try {
    if (typeof token === 'string') {
      const decoded = jwtDecode<JwtPayload>(token);
      return decoded.exp * 1000 < Date.now();
    }
    return token.expiresAt.getTime() < Date.now();
  } catch {
    return true;
  }
}

/**
 * Get time remaining until token expires (in seconds)
 * 
 * @param token JWT token or UserInfo
 * @returns Seconds until expiration, or 0 if already expired
 */
export function getTokenExpiresIn(token: string | UserInfo): number {
  try {
    if (typeof token === 'string') {
      const decoded = jwtDecode<JwtPayload>(token);
      const expiresAt = decoded.exp * 1000;
      const remainingMs = expiresAt - Date.now();
      return Math.max(0, Math.floor(remainingMs / 1000));
    }
    const remainingMs = token.expiresAt.getTime() - Date.now();
    return Math.max(0, Math.floor(remainingMs / 1000));
  } catch {
    return 0;
  }
}

/**
 * Role constants - predefined system roles
 */
export const SystemRoles = {
  ADMIN: 'admin',
  TENANT_ADMIN: 'tenant_admin',
  AGENT: 'agent',
  MANAGER: 'manager',
  PORTAL_USER: 'portal_user'
} as const;

/**
 * Permission matrix for role-based access control
 * Maps roles to feature permissions
 */
export const RolePermissions = {
  [SystemRoles.ADMIN]: [
    'view_dashboard',
    'manage_users',
    'manage_roles',
    'manage_tenants',
    'view_audit_log',
    'manage_system_settings'
  ],
  [SystemRoles.TENANT_ADMIN]: [
    'view_dashboard',
    'manage_users',
    'manage_roles',
    'view_audit_log'
  ],
  [SystemRoles.AGENT]: [
    'view_dashboard',
    'manage_tickets',
    'view_knowledge_base'
  ],
  [SystemRoles.MANAGER]: [
    'view_dashboard',
    'view_reports',
    'manage_team'
  ],
  [SystemRoles.PORTAL_USER]: [
    'view_dashboard',
    'create_ticket',
    'view_own_tickets'
  ]
} as const;

/**
 * Check if user has permission to perform an action
 * 
 * @param roles User roles
 * @param permission Permission to check
 * @returns true if user has permission
 */
export function hasPermission(
  roles: string[],
  permission: string
): boolean {
  return roles.some(role => {
    const permissions = RolePermissions[role as keyof typeof RolePermissions];
    return permissions?.some(allowedPermission => allowedPermission === permission) ?? false;
  });
}

/**
 * Get all permissions for user based on their roles
 * 
 * @param roles User roles
 * @returns Array of all permissions
 */
export function getUserPermissions(roles: string[]): string[] {
  const permissions = new Set<string>();
  
  for (const role of roles) {
    const rolePerms = RolePermissions[role as keyof typeof RolePermissions];
    if (rolePerms) {
      rolePerms.forEach(p => permissions.add(p));
    }
  }
  
  return Array.from(permissions).sort();
}
