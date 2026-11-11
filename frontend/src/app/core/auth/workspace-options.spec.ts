import { workspaceOptionsFor } from './workspace-options';

describe('workspaceOptionsFor', () => {
    it('returns engineer and admin workspaces in a stable order', () => {
        const options = workspaceOptionsFor(['admin', 'agent']);

        expect(options.map(option => option.role)).toEqual(['agent', 'admin']);
        expect(options[0].title).toBe('Интерфейс инженера');
        expect(options[1].title).toBe('Админка');
    });

    it('removes duplicates and ignores unknown roles from the backend', () => {
        const options = workspaceOptionsFor(['agent', 'agent', 'support', 'tenant_admin']);

        expect(options.map(option => option.role)).toEqual(['agent', 'tenant_admin']);
    });

    it('shows the tenant owner workspace separately from the local admin', () => {
        const options = workspaceOptionsFor(['tenant_owner', 'admin']);

        expect(options.map(option => option.role)).toEqual(['admin', 'tenant_owner']);
        expect(options[1].title).toBe('Владелец тенанта');
    });

    it('returns an empty list when no known roles are available', () => {
        expect(workspaceOptionsFor(['unknown'])).toEqual([]);
    });
});
