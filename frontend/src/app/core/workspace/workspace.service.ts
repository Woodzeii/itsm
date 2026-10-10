import { Injectable, inject } from '@angular/core';
import { WorkspaceApi } from './workspace-api';
import { CreateTicketRequest } from './workspace.models';

@Injectable({ providedIn: 'root' })
export class WorkspaceService {
    private readonly api = inject(WorkspaceApi);

    getOverview() { return this.api.getOverview(); }
    getTickets() { return this.api.getTickets(); }
    getAssets() { return this.api.getAssets(); }
    getServiceCategories() { return this.api.getServiceCategories(); }
    getEscalations() { return this.api.getEscalations(); }
    createTicket(request: CreateTicketRequest) { return this.api.createTicket(request); }
}