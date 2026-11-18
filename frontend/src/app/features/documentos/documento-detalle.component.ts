import { Component, ElementRef, Injector, OnInit, inject, input, signal, viewChild } from '@angular/core';
import { FormArray, FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { DocumentosService } from '../../core/documentos.service';
import { mensajeDeError } from '../../core/errores';
import { enfocarPrimerError } from '../../core/foco';
import { Campo, DocumentoDetalle, Mensaje } from '../../core/modelos';
import { formatearTamano, obligatorioSinEspacios } from '../../core/validadores';
import { AvisosService } from '../../shared/avisos.service';

const LARGO_MAXIMO_VALOR = 1000;
const LARGO_MAXIMO_PREGUNTA = 500;
const CONFIANZA_MINIMA = 0.8;

@Component({
  selector: 'app-documento-detalle',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './documento-detalle.component.html',
  styleUrl: './documento-detalle.component.scss',
})
export class DocumentoDetalleComponent implements OnInit {
  /** Llega desde la ruta /documentos/:id */
  readonly id = input.required<string>();

  private readonly servicio = inject(DocumentosService);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly listaMensajes = viewChild<ElementRef<HTMLElement>>('listaMensajes');

  protected readonly formatearTamano = formatearTamano;
  protected readonly maximoValor = LARGO_MAXIMO_VALOR;
  protected readonly maximoPregunta = LARGO_MAXIMO_PREGUNTA;

  protected readonly documento = signal<DocumentoDetalle | null>(null);
  protected readonly cargando = signal(true);
  protected readonly errorCarga = signal<string | null>(null);

  protected readonly guardando = signal(false);

  protected readonly mensajes = signal<Mensaje[]>([]);
  protected readonly pensando = signal(false);
  protected readonly errorPregunta = signal<string | null>(null);

  protected readonly formulario = this.fb.group({ campos: this.fb.array<FormControl<string>>([]) });
  protected readonly pregunta = this.fb.control('', [
    obligatorioSinEspacios,
    Validators.maxLength(LARGO_MAXIMO_PREGUNTA),
  ]);

  protected get controlesCampos(): FormControl<string>[] {
    return (this.formulario.controls.campos as FormArray<FormControl<string>>).controls;
  }

  ngOnInit(): void {
    this.cargar();
  }

  protected cargar(): void {
    this.cargando.set(true);
    this.errorCarga.set(null);

    this.servicio.obtener(Number(this.id())).subscribe({
      next: (documento) => {
        this.mostrar(documento);
        this.cargando.set(false);
      },
      error: (e: unknown) => {
        this.errorCarga.set(mensajeDeError(e));
        this.cargando.set(false);
      },
    });
  }

  protected confirmar(): void {
    if (this.guardando()) return;

    const formulario = this.formulario.controls.campos;
    if (formulario.invalid) {
      formulario.markAllAsTouched();
      enfocarPrimerError(this.injector, this.elemento.nativeElement);
      return;
    }

    const documento = this.documento();
    if (documento === null) return;

    this.guardando.set(true);

    const campos = documento.campos.map((campo, i) => ({
      codCampo: campo.codCampo,
      valor: this.controlesCampos[i].value.trim(),
    }));

    this.servicio.confirmarCampos(documento.codDocumento, campos).subscribe({
      next: (actualizado) => {
        this.mostrar(actualizado);
        this.guardando.set(false);
        this.avisos.exito('Datos confirmados', 'Tus correcciones quedaron guardadas.');
      },
      error: (e: unknown) => {
        this.guardando.set(false);
        this.avisos.error('No se pudieron guardar los datos', mensajeDeError(e));
      },
    });
  }

  protected enviarPregunta(evento?: Event): void {
    evento?.preventDefault();
    if (this.pensando()) return;

    if (this.pregunta.invalid) {
      this.pregunta.markAsTouched();
      return;
    }

    const documento = this.documento();
    if (documento === null) return;

    const texto = this.pregunta.value.trim();
    this.pregunta.reset('');
    this.errorPregunta.set(null);
    this.pensando.set(true);
    this.agregarMensaje('Usuario', texto);

    this.servicio.preguntar(documento.codDocumento, texto).subscribe({
      next: (r) => {
        this.agregarMensaje('Asistente', r.respuesta);
        this.pensando.set(false);
      },
      error: (e: unknown) => {
        // La pregunta no se guardó en el servidor: se devuelve a la caja para que se pueda reintentar.
        this.mensajes.update((lista) => lista.slice(0, -1));
        this.pregunta.setValue(texto);
        this.errorPregunta.set(mensajeDeError(e));
        this.pensando.set(false);
      },
    });
  }

  protected eliminar(): void {
    const documento = this.documento();
    if (documento === null) return;

    this.avisos
      .confirmar('¿Eliminar el documento?', `Se borrará «${documento.nombreArchivo}» con sus datos y su conversación. No se puede deshacer.`, 'Eliminar', true)
      .subscribe((confirmado) => {
        if (!confirmado) return;

        this.servicio.eliminar(documento.codDocumento).subscribe({
          next: () => {
            void this.router.navigate(['/documentos']);
            this.avisos.exito('Documento eliminado', 'El documento y su conversación se borraron.');
          },
          error: (e: unknown) => this.avisos.error('No se pudo eliminar', mensajeDeError(e)),
        });
      });
  }

  protected esDudoso(campo: Campo): boolean {
    return campo.valorConfirmado === null && campo.confianza < CONFIANZA_MINIMA;
  }

  protected porcentaje(campo: Campo): number {
    return Math.round(campo.confianza * 100);
  }

  protected claseEstado(estado: string): string {
    if (estado === 'Procesado') return 'estado estado--ok';
    if (estado === 'Con error') return 'estado estado--error';
    return 'estado';
  }

  private mostrar(documento: DocumentoDetalle): void {
    this.documento.set(documento);
    this.mensajes.set(documento.mensajes);

    const controles = this.formulario.controls.campos as FormArray<FormControl<string>>;
    controles.clear();
    for (const campo of documento.campos) {
      controles.push(
        this.fb.control(campo.valorConfirmado ?? campo.valor, [
          obligatorioSinEspacios,
          Validators.maxLength(LARGO_MAXIMO_VALOR),
        ]),
      );
    }
    this.formulario.markAsPristine();
    this.bajarConversacion();
  }

  private agregarMensaje(origen: Mensaje['origen'], texto: string): void {
    const ahora = new Date();
    this.mensajes.update((lista) => [
      ...lista,
      {
        codMensaje: -Date.now(),
        origen,
        texto,
        fecha: ahora.toLocaleDateString('es-PE'),
        hora: ahora.toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit', hour12: false }),
      },
    ]);
    this.bajarConversacion();
  }

  private bajarConversacion(): void {
    queueMicrotask(() => {
      const lista = this.listaMensajes()?.nativeElement;
      if (lista) lista.scrollTop = lista.scrollHeight;
    });
  }
}
