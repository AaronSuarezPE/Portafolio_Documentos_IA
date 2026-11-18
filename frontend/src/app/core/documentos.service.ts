import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CampoConfirmado, DocumentoDetalle, DocumentoResumen, Salud } from './modelos';

@Injectable({ providedIn: 'root' })
export class DocumentosService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  obtenerSalud(): Observable<Salud> {
    return this.http.get<Salud>(`${this.base}/Salud/Get`);
  }

  listar(): Observable<DocumentoResumen[]> {
    return this.http.get<DocumentoResumen[]>(`${this.base}/Documentos/GetList`);
  }

  obtener(id: number): Observable<DocumentoDetalle> {
    return this.http.get<DocumentoDetalle>(`${this.base}/Documentos/Get`, { params: this.porId(id) });
  }

  procesar(archivo: File): Observable<DocumentoDetalle> {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    return this.http.post<DocumentoDetalle>(`${this.base}/Documentos/PostProcesarDocumento`, formulario);
  }

  confirmarCampos(id: number, campos: CampoConfirmado[]): Observable<DocumentoDetalle> {
    return this.http.put<DocumentoDetalle>(
      `${this.base}/Documentos/PutActualizarCampos`,
      { campos },
      { params: this.porId(id) },
    );
  }

  preguntar(id: number, pregunta: string): Observable<{ respuesta: string }> {
    return this.http.post<{ respuesta: string }>(
      `${this.base}/Documentos/PostProcesarPregunta`,
      { pregunta },
      { params: this.porId(id) },
    );
  }

  eliminar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/Documentos/DeleteEliminar`, { params: this.porId(id) });
  }

  private porId(id: number): HttpParams {
    return new HttpParams().set('id', id);
  }
}
