import { Injectable, inject } from '@angular/core';
import { forkJoin, map, of, switchMap } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import { environment } from '../../../environments/environment';
import {
    AdminSettings,
    AssetDetails,
    AssetClassOption,
    AssetMovementRequest,
    AssetRecord,
    CreateAssetRequest,
    CreateTenantRequest,
    CreatePortalAccountRequest,
    CreateTicketRequest,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    FormFieldDefinition,
    OverviewData,
    PortalTicketRequest,
    RepairCloseRequest,
    ReportSummary,
    TicketComment,
    TicketFormDefinition,
    Ticket,
    TicketType,
    TenantRecord,
} from './workspace.models';

@Injectable()
export class HttpWorkspaceApiService extends WorkspaceApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;

    getOverview() { return this.http.get<OverviewData>(`${this.baseUrl}/workspace/overview`); }
    getTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/tickets`); }
    getAssets() { return this.http.get<AssetRecord[]>(`${this.baseUrl}/assets`); }
    getAssetClasses() {
        return this.http.get<{ id: number; code: string; name: string; isActive: boolean }[]>(`${this.baseUrl}/asset-classes`).pipe(
            switchMap(classes => {
                if (!classes.length) return of([] as AssetClassOption[]);
                const requests = classes.map(assetClass => this.http
                    .get<{ code: string; name: string; dataType: string; isRequired: boolean; defaultValue: string | null; options: string | null }[]>(`${this.baseUrl}/asset-classes/${assetClass.id}/attributes`)
                    .pipe(map(attributes => ({
                        id: assetClass.id,
                        code: assetClass.code,
                        name: assetClass.name,
                        active: assetClass.isActive,
                        attributes: attributes.map(attribute => ({
                            code: attribute.code,
                            name: attribute.name,
                            dataType: attribute.dataType,
                            required: attribute.isRequired,
                            defaultValue: attribute.defaultValue ?? undefined,
                            options: parseOptions(attribute.options),
                        })),
                    } satisfies AssetClassOption))));
                return forkJoin(requests);
            }),
        );
    }
    getEscalations() { return this.http.get<Escalation[]>(`${this.baseUrl}/escalations`); }
    createTicket(request: CreateTicketRequest) { return this.http.post<Ticket>(`${this.baseUrl}/tickets`, request); }
    getTicketTypes() { return this.http.get<TicketType[]>(`${this.baseUrl}/ticket-types`); }
    saveTicketTypes(ticketTypes: TicketType[]) { return this.http.put<TicketType[]>(`${this.baseUrl}/ticket-types`, ticketTypes); }
    changeTicketStatus(ticketId: string, status: string) { return this.http.patch<Ticket>(`${this.baseUrl}/tickets/${ticketId}/status`, { status }); }
    assignTicket(ticketId: string, assignee: string) { return this.http.patch<Ticket>(`${this.baseUrl}/tickets/${ticketId}/assignee`, { assignee }); }
    changeTicketPriority(ticketId: string, priority: string) { return this.http.patch<Ticket>(`${this.baseUrl}/tickets/${ticketId}/priority`, { priority }); }
    addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>) { return this.http.post<TicketComment>(`${this.baseUrl}/tickets/${ticketId}/comments`, comment); }
    escalateTicket(ticketId: string, initiator: string) { return this.http.post<EscalationHistoryEntry>(`${this.baseUrl}/tickets/${ticketId}/escalations`, { initiator }); }
    getAssetDetails(assetId: string) { return this.http.get<AssetDetails>(`${this.baseUrl}/assets/${assetId}`); }
    createAsset(request: CreateAssetRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets`, request); }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) { return this.http.patch<AssetDetails>(`${this.baseUrl}/assets/${assetId}`, changes); }
    deleteAsset(assetId: string) { return this.http.delete<void>(`${this.baseUrl}/assets/${assetId}`); }
    moveAsset(assetId: string, request: AssetMovementRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets/${assetId}/movements`, request); }
    createRepairTicket(assetId: string, title: string) { return this.http.post<Ticket>(`${this.baseUrl}/tickets/repairs`, { assetId, title }); }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) { return this.http.post<Ticket>(`${this.baseUrl}/tickets/${ticketId}/repair-close`, request); }
    getForms() { return this.http.get<TicketFormDefinition[]>(`${this.baseUrl}/ticket-forms`); }
    saveForm(form: TicketFormDefinition) { return this.http.put<TicketFormDefinition>(`${this.baseUrl}/ticket-forms/${form.ticketTypeId}`, form); }
    getEscalationPolicies() { return this.http.get<EscalationPolicy[]>(`${this.baseUrl}/escalation-policies`); }
    saveEscalationPolicies(policies: EscalationPolicy[]) { return this.http.put<EscalationPolicy[]>(`${this.baseUrl}/escalation-policies`, policies); }
    getEscalationHistory() { return this.http.get<EscalationHistoryEntry[]>(`${this.baseUrl}/escalations/history`); }
    getAdminSettings() { return this.http.get<AdminSettings>(`${this.baseUrl}/settings`); }
    saveAdminSettings(settings: AdminSettings) { return this.http.put<AdminSettings>(`${this.baseUrl}/settings`, settings); }
    getReports(from?: string, to?: string) { return this.http.get<ReportSummary[]>(`${this.baseUrl}/reports`, { params: { ...(from ? { from } : {}), ...(to ? { to } : {}) } }); }
    getPortalTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/portal/tickets`); }
    createPortalTicket(request: PortalTicketRequest) {
        const formData = new FormData();
        formData.append('payload', JSON.stringify({ ...request, attachments: undefined }));
        for (const file of request.attachments ?? []) formData.append('attachments', file);
        return this.http.post<Ticket>(`${this.baseUrl}/portal/tickets`, formData);
    }
    addPortalComment(ticketId: string, message: string, attachments: File[]) {
        const formData = new FormData();
        formData.append('message', message);
        for (const file of attachments) formData.append('attachments', file);
        return this.http.post<TicketComment>(`${this.baseUrl}/portal/tickets/${ticketId}/comments`, formData);
    }
    reviewPortalTicket(ticketId: string, accepted: boolean) { return this.http.post<Ticket>(`${this.baseUrl}/portal/tickets/${ticketId}/review`, { accepted }); }
    createPortalAccount(request: CreatePortalAccountRequest) { return this.http.post<string>(`${this.baseUrl}/portal/accounts`, request); }
    getTenants() { return this.http.get<TenantRecord[]>(`${this.baseUrl}/tenants`); }
    createTenant(request: CreateTenantRequest) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants`, request); }
    setTenantActive(tenantId: string, active: boolean) { return this.http.patch<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}/status`, { active }); }
    deleteTenant(tenantId: string) { return this.http.delete<void>(`${this.baseUrl}/tenants/${tenantId}`); }
}

function parseOptions(value: string | null): string[] {
    if (!value) return [];
    try {
        const parsed: unknown = JSON.parse(value);
        if (Array.isArray(parsed)) return parsed.map(String);
    } catch {
        return value.split(',').map(option => option.trim()).filter(Boolean);
    }
    return [];
}