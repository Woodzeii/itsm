import { Provider } from '@angular/core';
import { WorkspaceApi } from './workspace-api';
import { HttpWorkspaceApiService } from './http-workspace-api.service';

export const workspaceApiProvider: Provider = {
    provide: WorkspaceApi,
    useClass: HttpWorkspaceApiService,
};
