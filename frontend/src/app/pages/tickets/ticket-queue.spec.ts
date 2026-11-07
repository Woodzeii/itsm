import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Ticket } from '../../core/workspace/workspace.models';
import { TicketQueueComponent } from './ticket-queue';

describe('TicketQueueComponent real queue', () => {
    const openTicket: Ticket = {
        id: '42',
        title: 'Не подключается VPN',
        requester: 'Иван Иванов',
        team: '',
        priority: 'Высокий',
        status: 'Открыта',
        createdAt: '2026-10-31T10:00:00Z',
        slaRemaining: '2 ч',
        type: 'Инцидент',
        statusId: 1,
    };
    const assignedTicket: Ticket = {
        ...openTicket,
        id: '43',
        title: 'Не работает принтер',
        status: 'В работе',
        statusId: 3,
        assignee: 'Пётр Петров',
        assigneeId: 5,
    };

    let api: {
        getTickets: ReturnType<typeof vi.fn>;
        knownAssigneeNames: ReturnType<typeof vi.fn>;
        resolveStatusId: ReturnType<typeof vi.fn>;
        resolveAssigneeId: ReturnType<typeof vi.fn>;
        changeTicketStatus: ReturnType<typeof vi.fn>;
        assignTicket: ReturnType<typeof vi.fn>;
        getPortalTicketTypes: ReturnType<typeof vi.fn>;
        createPortalTicket: ReturnType<typeof vi.fn>;
    };
    let queryParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

    beforeEach(async () => {
        queryParams = new BehaviorSubject(convertToParamMap({}));
        api = {
            getTickets: vi.fn().mockReturnValue(of([openTicket, assignedTicket])),
            knownAssigneeNames: vi.fn().mockReturnValue(['Пётр Петров']),
            resolveStatusId: vi.fn((name: string) => name === 'В работе' ? 3 : undefined),
            resolveAssigneeId: vi.fn((name: string) => name === 'Пётр Петров' ? 5 : undefined),
            changeTicketStatus: vi.fn().mockReturnValue(of({ ...openTicket, status: 'В работе', statusId: 3 })),
            assignTicket: vi.fn().mockReturnValue(of({ ...openTicket, assignee: 'Пётр Петров', assigneeId: 5 })),
            getPortalTicketTypes: vi.fn().mockReturnValue(of([{ id: 1, name: 'Инцидент' }])),
            createPortalTicket: vi.fn().mockReturnValue(of({ ...openTicket, id: '44', title: 'Новая заявка' })),
        };

        await TestBed.configureTestingModule({
            imports: [TicketQueueComponent],
            providers: [
                { provide: HttpWorkspaceApiService, useValue: api },
                {
                    provide: WorkspaceService,
                    useValue: {
                        getAdminSettings: () => of({
                            assignmentMode: 'manual',
                            engineers: ['Пётр Петров'],
                            criticalities: ['Высокий', 'Средний'],
                            statuses: [
                                { name: 'Открыта', pausesSla: false, immutable: true },
                                { name: 'Ожидает выполнения', pausesSla: false, immutable: true },
                                { name: 'В работе', pausesSla: false, immutable: true },
                            ],
                            statusTransitions: {
                                'Открыта': ['В работе'],
                                'В работе': [],
                            },
                        }),
                    },
                },
                {
                    provide: AuthService,
                    useValue: { canRead: () => true, getLogin: () => 'agent' },
                },
                {
                    provide: ActivatedRoute,
                    useValue: { queryParamMap: queryParams.asObservable() },
                },
            ],
        }).compileComponents();
    });

    function createComponent(): TicketQueueComponent {
        const fixture = TestBed.createComponent(TicketQueueComponent);
        fixture.detectChanges();
        return fixture.componentInstance;
    }

    it('loads the engineer queue from GET /api/tickets through the HTTP service', () => {
        const component = createComponent();

        expect(api.getTickets).toHaveBeenCalled();
        expect(component.tickets.map(ticket => ticket.id)).toEqual(['42', '43']);
    });

    it('filters the real queue by status and assignee', () => {
        const component = createComponent();

        component.statusFilter = 'В работе';
        expect(component.filteredTickets.map(ticket => ticket.id)).toEqual(['43']);

        component.statusFilter = '';
        component.assigneeFilter = 'Пётр Петров';
        expect(component.filteredTickets.map(ticket => ticket.id)).toEqual(['43']);
    });

    it('builds status buttons with real status ids and calls PUT status endpoint helper', () => {
        const component = createComponent();
        component.select(openTicket);

        expect(component.nextStatuses).toEqual([{ name: 'В работе', statusId: 3 }]);

        component.changeStatus(3);

        expect(api.changeTicketStatus).toHaveBeenCalledWith('42', 3);
    });

    it('resolves the assignee id from the real queue and assigns the ticket', () => {
        const component = createComponent();
        component.select(openTicket);
        component.assigneeDraft = 'Пётр Петров';

        component.assign();

        expect(api.resolveAssigneeId).toHaveBeenCalledWith('Пётр Петров');
        expect(api.assignTicket).toHaveBeenCalledWith('42', 5);
    });

    it('opens the creation form from the overview create query parameter', () => {
        const component = createComponent();
        queryParams.next(convertToParamMap({ create: 'true' }));

        expect(component.showCreateForm).toBe(true);
        expect(api.getPortalTicketTypes).toHaveBeenCalled();
        expect(component.newTicketTypeId).toBe('1');
    });

    it('creates a ticket from the queue form and adds it to the queue', () => {
        const component = createComponent();
        component.openCreate();
        component.newTicketTitle = 'Новая заявка';
        component.newTicketDescription = 'Подробное описание';

        component.createTicket();

        expect(api.createPortalTicket).toHaveBeenCalledWith({
            title: 'Новая заявка',
            description: 'Подробное описание',
            requester: 'agent',
            type: 'Инцидент',
            typeId: 1,
            priority: 'Средний',
        });
        expect(component.tickets[0].id).toBe('44');
        expect(component.showCreateForm).toBe(false);
    });
});
