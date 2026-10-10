import { Injectable, inject } from '@angular/core';
import { WorkspaceApi } from './workspace-api';
import {
    AdminSettings,
    AssetMovementRequest,
    AssetClassOption,
    CreateAssetRequest,
    CreatePortalAccountRequest,
    CreateTenantRequest,
    CreateTicketRequest,
    EscalationPolicy,
    PortalTicketRequest,
    RepairCloseRequest,
    TicketFormDefinition,
    TicketType,
    TenantRecord,
} from './workspace.models';

@Injectable({ providedIn: 'root' })
export class WorkspaceService {
    private readonly api = inject(WorkspaceApi);

    getOverview() { return this.api.getOverview(); }
    getTickets() { return this.api.getTickets(); }
    getAssets() { return this.api.getAssets(); }
    getAssetClasses() { return this.api.getAssetClasses(); }
    getEscalations() { return this.api.getEscalations(); }
    createTicket(request: CreateTicketRequest) { return this.api.createTicket(request); }
    getTicketTypes() { return this.api.getTicketTypes(); }
    saveTicketTypes(types: TicketType[]) { return this.api.saveTicketTypes(types); }
    changeTicketStatus(ticketId: string, status: string) { return this.api.changeTicketStatus(ticketId, status); }
    assignTicket(ticketId: string, assignee: string) { return this.api.assignTicket(ticketId, assignee); }
    changeTicketPriority(ticketId: string, priority: string) { return this.api.changeTicketPriority(ticketId, priority); }
    addTicketComment(ticketId: string, comment: Parameters<typeof this.api.addTicketComment>[1]) { return this.api.addTicketComment(ticketId, comment); }
    escalateTicket(ticketId: string, initiator: string) { return this.api.escalateTicket(ticketId, initiator); }
    getAssetDetails(assetId: string) { return this.api.getAssetDetails(assetId); }
    createAsset(request: CreateAssetRequest) { return this.api.createAsset(request); }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) { return this.api.updateAsset(assetId, changes); }
    deleteAsset(assetId: string) { return this.api.deleteAsset(assetId); }
    moveAsset(assetId: string, request: AssetMovementRequest) { return this.api.moveAsset(assetId, request); }
    createRepairTicket(assetId: string, title: string) { return this.api.createRepairTicket(assetId, title); }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) { return this.api.closeRepairTicket(ticketId, request); }
    getForms() { return this.api.getForms(); }
    saveForm(form: TicketFormDefinition) { return this.api.saveForm(form); }
    getEscalationPolicies() { return this.api.getEscalationPolicies(); }
    saveEscalationPolicies(policies: EscalationPolicy[]) { return this.api.saveEscalationPolicies(policies); }
    getEscalationHistory() { return this.api.getEscalationHistory(); }
    getAdminSettings() { return this.api.getAdminSettings(); }
    saveAdminSettings(settings: AdminSettings) { return this.api.saveAdminSettings(settings); }
    getReports(from?: string, to?: string) { return this.api.getReports(from, to); }
    getPortalTickets() { return this.api.getPortalTickets(); }
    createPortalTicket(request: PortalTicketRequest) { return this.api.createPortalTicket(request); }
    addPortalComment(ticketId: string, message: string, attachments: File[]) { return this.api.addPortalComment(ticketId, message, attachments); }
    reviewPortalTicket(ticketId: string, accepted: boolean) { return this.api.reviewPortalTicket(ticketId, accepted); }
    createPortalAccount(request: CreatePortalAccountRequest) { return this.api.createPortalAccount(request); }
    getTenants() { return this.api.getTenants(); }
    createTenant(request: CreateTenantRequest) { return this.api.createTenant(request); }
    setTenantActive(tenantId: string, active: boolean) { return this.api.setTenantActive(tenantId, active); }
    deleteTenant(tenantId: string) { return this.api.deleteTenant(tenantId); }
}