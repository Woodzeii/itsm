import { Provider } from '@angular/core';
import { WorkspaceApi } from './workspace-api';
import { MockWorkspaceApiService } from './mock-workspace-api.service';
import { HttpWorkspaceApiService } from './http-workspace-api.service';
import { environment } from '../../../environments/environment';

export const workspaceApiProvider: Provider = {
    provide: WorkspaceApi,
    useClass: environment.useMockWorkspaceApi
        ? MockWorkspaceApiService
        : HttpWorkspaceApiService,
};
