import { CanDeactivateFn } from '@angular/router';

export interface PendingChangesComponent {
    canDeactivate(): boolean;
}

export const pendingChangesGuard: CanDeactivateFn<PendingChangesComponent> = component =>
    typeof component?.canDeactivate === 'function' ? component.canDeactivate() : true;