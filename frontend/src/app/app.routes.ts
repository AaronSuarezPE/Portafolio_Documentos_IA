import { Routes } from '@angular/router';
import { authGuard, sinSesionGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'acceso',
    canActivate: [sinSesionGuard],
    title: 'Ingresar · DocuIA',
    loadComponent: () => import('./features/acceso/acceso.component').then((m) => m.AccesoComponent),
  },
  {
    path: 'documentos',
    canActivate: [authGuard],
    title: 'Mis documentos · DocuIA',
    loadComponent: () => import('./features/documentos/documentos.component').then((m) => m.DocumentosComponent),
  },
  {
    path: 'documentos/:id',
    canActivate: [authGuard],
    title: 'Documento · DocuIA',
    loadComponent: () =>
      import('./features/documentos/documento-detalle.component').then((m) => m.DocumentoDetalleComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'documentos' },
  { path: '**', redirectTo: 'documentos' },
];
