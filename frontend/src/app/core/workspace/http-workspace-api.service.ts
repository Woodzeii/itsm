import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import {
    AssetRecord,
    CreateTicketRequest,
    Escalation,
    OverviewData,
    ServiceCategory,
    Ticket,
} from './workspace.models';

@Injectable()
export class HttpWorkspaceApiService extends WorkspaceApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = '/api';

    getOverview() { return this.http.get<OverviewData>(`${this.baseUrl}/workspace/overview`); }
    getTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/tickets`); }
    getAssets() { return this.http.get<AssetRecord[]>(`${this.baseUrl}/assets`); }
    getServiceCategories() { return this.http.get<ServiceCategory[]>(`${this.baseUrl}/service-catalog`); }
    getEscalations() { return this.http.get<Escalation[]>(`${this.baseUrl}/escalations`); }
    createTicket(request: CreateTicketRequest) { return this.http.post<Ticket>(`${this.baseUrl}/tickets`, request); }
}