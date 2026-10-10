import { Observable } from 'rxjs';
import {
    AssetRecord,
    CreateTicketRequest,
    Escalation,
    OverviewData,
    ServiceCategory,
    Ticket,
} from './workspace.models';

export abstract class WorkspaceApi {
    abstract getOverview(): Observable<OverviewData>;
    abstract getTickets(): Observable<Ticket[]>;
    abstract getAssets(): Observable<AssetRecord[]>;
    abstract getServiceCategories(): Observable<ServiceCategory[]>;
    abstract getEscalations(): Observable<Escalation[]>;
    abstract createTicket(request: CreateTicketRequest): Observable<Ticket>;
}