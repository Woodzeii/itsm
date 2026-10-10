import { Provider } from '@angular/core';
import { WorkspaceApi } from './workspace-api';
import { MockWorkspaceApiService } from './mock-workspace-api.service';

export const workspaceApiProvider: Provider = {
    provide: WorkspaceApi,
    useClass: MockWorkspaceApiService,
};
