import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { mensajeDeError } from '../../core/errores';
import { DocumentoResumen } from '../../core/modelos';
import { DocumentosService } from '../../core/documentos.service';
import {
  TAMANO_MAXIMO_MB,
  formatearTamano,
  validarArchivo,
  validarContenido,
} from '../../core/validadores';
import { AvisosService } from '../../shared/avisos.service';
import { PanelLateralComponent } from '../../shared/panel-lateral.component';

@Component({
  selector: 'app-documentos',
  imports: [RouterLink, MatButtonModule, MatProgressBarModule, PanelLateralComponent],
  templateUrl: './documentos.component.html',
  styleUrl: './documentos.component.scss',
})
export class DocumentosComponent implements OnInit {
  private readonly servicio = inject(DocumentosService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);

  protected readonly formatearTamano = formatearTamano;
  protected readonly maximoMb = TAMANO_MAXIMO_MB;

  protected readonly documentos = signal<DocumentoResumen[]>([]);
  protected readonly cargando = signal(true);
  protected readonly errorLista = signal<string | null>(null);
  protected readonly modoDemostracion = signal(false);

  protected readonly panelAbierto = signal(false);
  protected readonly archivo = signal<File | null>(null);
  protected readonly errorArchivo = signal<string | null>(null);
  protected readonly subiendo = signal(false);
  protected readonly arrastrando = signal(false);

  ngOnInit(): void {
    this.cargar();
    // El aviso de demostración es informativo: si falla, la pantalla funciona igual.
    this.servicio.obtenerSalud().subscribe({
      next: (s) => this.modoDemostracion.set(s.modoDemostracion),
      error: () => undefined,
    });
  }

  protected cargar(): void {
    this.cargando.set(true);
    this.errorLista.set(null);

    this.servicio.listar().subscribe({
      next: (lista) => {
        this.documentos.set(lista);
        this.cargando.set(false);
      },
      error: (e: unknown) => {
        this.errorLista.set(mensajeDeError(e));
        this.cargando.set(false);
      },
    });
  }

  protected abrirPanel(): void {
    this.reiniciarFormulario();
    this.panelAbierto.set(true);
  }

  protected cerrarPanel(): void {
    if (this.subiendo()) return;

    if (this.archivo() === null) {
      this.panelAbierto.set(false);
      return;
    }

    this.avisos
      .confirmar('¿Salir sin analizar?', 'Elegiste un archivo que todavía no se analizó. Si sales, se descarta.', 'Salir')
      .subscribe((salir) => {
        if (salir) this.panelAbierto.set(false);
      });
  }

  protected alElegir(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const elegidos = Array.from(entrada.files ?? []);
    entrada.value = '';
    void this.recibir(elegidos);
  }

  protected alSoltar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(false);
    void this.recibir(Array.from(evento.dataTransfer?.files ?? []));
  }

  protected alArrastrar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(true);
  }

  protected quitarArchivo(): void {
    this.archivo.set(null);
    this.errorArchivo.set(null);
  }

  protected analizar(): void {
    if (this.subiendo()) return;

    const archivo = this.archivo();
    if (archivo === null) {
      this.errorArchivo.set('Adjunta el documento que quieres analizar.');
      return;
    }

    this.subiendo.set(true);

    this.servicio.procesar(archivo).subscribe({
      next: (documento) => {
        this.subiendo.set(false);
        this.panelAbierto.set(false);
        void this.router.navigate(['/documentos', documento.codDocumento]);
        this.avisos.exito('Documento analizado', 'Revisa los datos que encontró la IA y confírmalos.');
      },
      error: (e: unknown) => {
        this.subiendo.set(false);
        this.avisos.error('No se pudo analizar el documento', mensajeDeError(e));
        this.cargar();
      },
    });
  }

  protected eliminar(documento: DocumentoResumen): void {
    this.avisos
      .confirmar('¿Eliminar el documento?', `Se borrará «${documento.nombreArchivo}» con sus datos y su conversación. No se puede deshacer.`, 'Eliminar', true)
      .subscribe((confirmado) => {
        if (!confirmado) return;

        this.servicio.eliminar(documento.codDocumento).subscribe({
          next: () => {
            this.cargar();
            this.avisos.exito('Documento eliminado', 'El documento y su conversación se borraron.');
          },
          error: (e: unknown) => this.avisos.error('No se pudo eliminar', mensajeDeError(e)),
        });
      });
  }

  protected claseEstado(estado: string): string {
    if (estado === 'Procesado') return 'estado estado--ok';
    if (estado === 'Con error') return 'estado estado--error';
    return 'estado';
  }

  private async recibir(archivos: File[]): Promise<void> {
    if (archivos.length === 0) return;

    if (archivos.length > 1) {
      this.archivo.set(null);
      this.errorArchivo.set('Solo se puede analizar un documento a la vez. Elige uno.');
      return;
    }

    const archivo = archivos[0];
    const error = validarArchivo(archivo) ?? (await validarContenido(archivo));
    if (error) {
      this.archivo.set(null);
      this.errorArchivo.set(error);
      return;
    }

    this.errorArchivo.set(null);
    this.archivo.set(archivo);
  }

  private reiniciarFormulario(): void {
    this.archivo.set(null);
    this.errorArchivo.set(null);
    this.arrastrando.set(false);
  }
}
