import { Observable } from 'rxjs';
import {
    AssetRecord,
    AssetDetails,
    AssetMovementRequest,
    AssetClassOption,
    CreateTicketRequest,
    CreateAssetRequest,
    CreatePortalAccountRequest,
    CreateTenantRequest,
    AdminSettings,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    FormFieldDefinition,
    OverviewData,
    PortalTicketRequest,
    RepairCloseRequest,
    ReportSummary,
    TicketFormDefinition,
    Ticket,
    TicketComment,
    TicketType,
    TenantRecord,
} from './workspace.models';

export abstract class WorkspaceApi {
    abstract getOverview(): Observable<OverviewData>;
    abstract getTickets(): Observable<Ticket[]>;
    abstract getAssets(): Observable<AssetRecord[]>;
    abstract getAssetClasses(): Observable<AssetClassOption[]>;
    abstract getEscalations(): Observable<Escalation[]>;
    abstract createTicket(request: CreateTicketRequest): Observable<Ticket>;
    abstract getTicketTypes(): Observable<TicketType[]>;
    abstract saveTicketTypes(ticketTypes: TicketType[]): Observable<TicketType[]>;
    abstract changeTicketStatus(ticketId: string, status: string): Observable<Ticket>;
    abstract assignTicket(ticketId: string, assignee: string): Observable<Ticket>;
    abstract changeTicketPriority(ticketId: string, priority: string): Observable<Ticket>;
    abstract addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>): Observable<TicketComment>;
    abstract escalateTicket(ticketId: string, initiator: string): Observable<EscalationHistoryEntry>;
    abstract getAssetDetails(assetId: string): Observable<AssetDetails>;
    abstract createAsset(request: CreateAssetRequest): Observable<AssetDetails>;
    abstract updateAsset(assetId: string, changes: Partial<CreateAssetRequest>): Observable<AssetDetails>;
    abstract deleteAsset(assetId: string): Observable<void>;
    abstract moveAsset(assetId: string, request: AssetMovementRequest): Observable<AssetDetails>;
    abstract createRepairTicket(assetId: string, title: string): Observable<Ticket>;
    abstract closeRepairTicket(ticketId: string, request: RepairCloseRequest): Observable<Ticket>;
    abstract getForms(): Observable<TicketFormDefinition[]>;
    abstract saveForm(form: TicketFormDefinition): Observable<TicketFormDefinition>;
    abstract getEscalationPolicies(): Observable<EscalationPolicy[]>;
    abstract saveEscalationPolicies(policies: EscalationPolicy[]): Observable<EscalationPolicy[]>;
    abstract getEscalationHistory(): Observable<EscalationHistoryEntry[]>;
    abstract getAdminSettings(): Observable<AdminSettings>;
    abstract saveAdminSettings(settings: AdminSettings): Observable<AdminSettings>;
    abstract getReports(from?: string, to?: string): Observable<ReportSummary[]>;
    abstract getPortalTickets(): Observable<Ticket[]>;
    abstract createPortalTicket(request: PortalTicketRequest): Observable<Ticket>;
    abstract addPortalComment(ticketId: string, message: string, attachments: File[]): Observable<TicketComment>;
    abstract reviewPortalTicket(ticketId: string, accepted: boolean): Observable<Ticket>;
    abstract createPortalAccount(request: CreatePortalAccountRequest): Observable<string>;
    abstract getTenants(): Observable<TenantRecord[]>;
    abstract createTenant(request: CreateTenantRequest): Observable<TenantRecord>;
    abstract setTenantActive(tenantId: string, active: boolean): Observable<TenantRecord>;
    abstract deleteTenant(tenantId: string): Observable<void>;
}