import { Provider } from '@angular/core';
import { AdminApi } from './admin-api';
import { MockAdminApiService } from './mock-admin-api.service';
import { HttpAdminApiService } from './http-admin-api.service';
import { environment } from '../../../environments/environment';

export const adminApiProvider: Provider = {
    provide: AdminApi,
    useClass: environment.useMockAdminApi
        ? MockAdminApiService
        : HttpAdminApiService,
};