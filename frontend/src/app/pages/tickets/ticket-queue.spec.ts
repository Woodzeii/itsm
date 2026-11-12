import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Ticket, TicketComment } from '../../core/workspace/workspace.models';
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
        getTicketComments: ReturnType<typeof vi.fn>;
        getTicketCommentsPage: ReturnType<typeof vi.fn>;
        getTicketHistory: ReturnType<typeof vi.fn>;
        getTicketHistoryPage: ReturnType<typeof vi.fn>;
        addTicketComment: ReturnType<typeof vi.fn>;
        getPortalTicketTypes: ReturnType<typeof vi.fn>;
        getRequesters: ReturnType<typeof vi.fn>;
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
            getTicketComments: vi.fn().mockReturnValue(of([])),
            getTicketCommentsPage: vi.fn().mockReturnValue(of([])),
            getTicketHistory: vi.fn().mockReturnValue(of([])),
            getTicketHistoryPage: vi.fn().mockReturnValue(of([])),
            addTicketComment: vi.fn().mockReturnValue(of({
                id: 'm3',
                author: 'agent',
                message: 'Проверяю',
                createdAt: '2026-11-07T08:00:00Z',
                internal: true,
                attachments: [],
            } satisfies TicketComment)),
            getPortalTicketTypes: vi.fn().mockReturnValue(of([{ id: 1, name: 'Инцидент' }])),
            getRequesters: vi.fn().mockReturnValue(of([
                { id: 7, username: 'anna.belova', fullName: 'Анна Белова', department: 'Бухгалтерия', email: 'anna@itsm.local' },
            ])),
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
                        getRequesters: () => of([
                            { id: 7, username: 'anna.belova', fullName: 'Анна Белова', department: 'Бухгалтерия', email: 'anna@itsm.local' },
                        ]),
                    },
                },
                {
                    provide: AuthService,
                    useValue: { canRead: () => true, getLogin: () => 'agent', getRole: () => 'agent' },
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

    it('loads the selected ticket conversation and retains internal message flags for staff', () => {
        const messages: TicketComment[] = [
            { id: 'm1', author: 'agent', message: 'Внутренняя заметка', createdAt: '', internal: true, attachments: [] },
            { id: 'm2', author: 'Иван', message: 'Публичный ответ', createdAt: '', internal: false, attachments: [] },
        ];
        api.getTicketCommentsPage.mockReturnValue(of(messages));
        const component = createComponent();

        component.select(openTicket);

        expect(api.getTicketCommentsPage).toHaveBeenCalledWith('42', 0, 30);
        expect(component.visibleComments).toEqual(messages);
    });

    it('opens the full history and conversation in separate activity panels', () => {
        const fixture = TestBed.createComponent(TicketQueueComponent);
        const component = fixture.componentInstance;
        fixture.detectChanges();
        component.select(openTicket);

        component.openActivityPanel('history');
        fixture.detectChanges();
        expect(component.activityPanel).toBe('history');
        expect(fixture.nativeElement.querySelector('#ticket-history-dialog-title')?.textContent.trim()).toBe('История заявки');

        component.openActivityPanel('comments');
        fixture.detectChanges();
        expect(component.activityPanel).toBe('comments');
        expect(fixture.nativeElement.querySelector('#ticket-comments-dialog-title')?.textContent.trim()).toBe('Переписка');

        component.closeActivityPanel();
        fixture.detectChanges();
        expect(component.activityPanel).toBeNull();
        expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    });

    it('filters internal comments when the active user is not staff', () => {
        const fixture = TestBed.createComponent(TicketQueueComponent);
        const component = fixture.componentInstance;
        component.canUseInternalComments = false;
        component.selected = {
            ...openTicket,
            comments: [
                { id: 'm1', author: 'agent', message: 'Внутренняя заметка', createdAt: '', internal: true, attachments: [] },
                { id: 'm2', author: 'Иван', message: 'Публичный ответ', createdAt: '', internal: false, attachments: [] },
            ],
        };

        expect(component.visibleComments.map(comment => comment.id)).toEqual(['m2']);
    });

    it('opens the creation form from the overview create query parameter', () => {
        const component = createComponent();
        queryParams.next(convertToParamMap({ create: 'true' }));

        expect(component.showCreateForm).toBe(true);
        expect(api.getPortalTicketTypes).toHaveBeenCalled();
        expect(component.newTicketTypeId).toBe('1');
    });

    it('loads requester options for staff so a ticket can be created on behalf of a user', () => {
        const component = createComponent();
        component.openCreate();

        expect(api.getRequesters).toHaveBeenCalled();
        expect(component.requesters.map(requester => requester.id)).toEqual([7]);
    });

    it('passes the selected requester when a staff member creates a ticket on behalf of a user', () => {
        const component = createComponent();
        component.openCreate();
        component.newTicketTitle = 'Не работает 1С';
        component.newTicketDescription = 'Бухгалтер не может открыть базу';
        component.newTicketRequesterId = '7';

        component.createTicket();

        expect(api.createPortalTicket).toHaveBeenCalledWith(expect.objectContaining({
            typeId: 1,
            requesterId: 7,
        }));
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
