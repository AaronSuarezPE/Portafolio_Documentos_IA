import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DocumentosService } from '../../core/documentos.service';
import { DocumentoDetalle } from '../../core/modelos';
import { AvisosService } from '../../shared/avisos.service';
import { DocumentoDetalleComponent } from './documento-detalle.component';

const documento = (): DocumentoDetalle => ({
  codDocumento: 5,
  nombreArchivo: 'factura.pdf',
  tipo: 'Factura',
  estado: 'Procesado',
  tamanoBytes: 1200,
  fecha: '17/11/2026',
  hora: '10:00',
  resumen: 'Factura de ejemplo',
  campos: [
    { codCampo: 1, clave: 'emisor', etiqueta: 'Emisor', valor: 'Comercial Lima', valorConfirmado: null, confianza: 0.95 },
    { codCampo: 2, clave: 'total', etiqueta: 'Total', valor: '118.00', valorConfirmado: null, confianza: 0.5 },
  ],
  mensajes: [],
});

describe('DocumentoDetalleComponent', () => {
  let fixture: ComponentFixture<DocumentoDetalleComponent>;
  let componente: any;
  let servicio: jasmine.SpyObj<DocumentosService>;
  let avisos: jasmine.SpyObj<AvisosService>;

  beforeEach(async () => {
    servicio = jasmine.createSpyObj<DocumentosService>('DocumentosService', [
      'obtener', 'confirmarCampos', 'preguntar', 'eliminar',
    ]);
    servicio.obtener.and.returnValue(of(documento()));
    avisos = jasmine.createSpyObj<AvisosService>('AvisosService', ['exito', 'error', 'info', 'confirmar']);

    await TestBed.configureTestingModule({
      imports: [DocumentoDetalleComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: DocumentosService, useValue: servicio },
        { provide: AvisosService, useValue: avisos },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentoDetalleComponent);
    fixture.componentRef.setInput('id', '5');
    componente = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('muestra los datos y marca como "Revisar" solo los de baja confianza', () => {
    const texto = fixture.nativeElement.textContent;

    expect(texto).toContain('Emisor');
    expect(texto).toContain('Revisar · 50%');
    expect(texto).not.toContain('Revisar · 95%');
  });

  it('no guarda si un dato obligatorio queda vacío o con solo espacios', () => {
    componente.controlesCampos[0].setValue('   ');

    componente.confirmar();

    expect(servicio.confirmarCampos).not.toHaveBeenCalled();
    expect(componente.controlesCampos[0].touched).toBeTrue();
  });

  it('no guarda un dato que supera el largo máximo', () => {
    componente.controlesCampos[1].setValue('x'.repeat(1001));

    componente.confirmar();

    expect(servicio.confirmarCampos).not.toHaveBeenCalled();
  });

  it('guarda los valores sin espacios sobrantes y avisa en el centro', () => {
    componente.controlesCampos[0].setValue('  Comercial Andina  ');
    servicio.confirmarCampos.and.returnValue(of(documento()));

    componente.confirmar();

    expect(servicio.confirmarCampos).toHaveBeenCalledWith(5, [
      { codCampo: 1, valor: 'Comercial Andina' },
      { codCampo: 2, valor: '118.00' },
    ]);
    expect(avisos.exito).toHaveBeenCalledWith('Datos confirmados', jasmine.any(String));
  });

  it('si el servidor falla muestra el motivo en un aviso central y permite reintentar', () => {
    servicio.confirmarCampos.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );

    componente.confirmar();

    expect(avisos.error).toHaveBeenCalledWith('No se pudieron guardar los datos', jasmine.stringContaining('servidor'));
    expect(componente.guardando()).toBeFalse();
  });

  it('no envía una pregunta vacía', () => {
    componente.pregunta.setValue('   ');

    componente.enviarPregunta();

    expect(servicio.preguntar).not.toHaveBeenCalled();
    expect(componente.pregunta.touched).toBeTrue();
  });

  it('muestra la pregunta y la respuesta en la conversación', () => {
    servicio.preguntar.and.returnValue(of({ respuesta: 'El total es 118.00' }));
    componente.pregunta.setValue('¿Cuál es el total?');

    componente.enviarPregunta();

    const textos = componente.mensajes().map((m: any) => m.texto);
    expect(textos).toEqual(['¿Cuál es el total?', 'El total es 118.00']);
    expect(componente.pregunta.value).toBe('');
  });

  it('si falla la pregunta la devuelve a la caja y avisa', () => {
    servicio.preguntar.and.returnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
    componente.pregunta.setValue('¿Cuál es el total?');

    componente.enviarPregunta();
    fixture.detectChanges();

    expect(componente.pregunta.value).toBe('¿Cuál es el total?');
    expect(componente.mensajes().length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('demasiadas solicitudes');
  });

  it('elimina solo si se confirma y vuelve a la lista', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    avisos.confirmar.and.returnValue(of(true));
    servicio.eliminar.and.returnValue(of(undefined));

    componente.eliminar();

    expect(servicio.eliminar).toHaveBeenCalledWith(5);
    expect(router.navigate).toHaveBeenCalledWith(['/documentos']);
    expect(avisos.exito).toHaveBeenCalledWith('Documento eliminado', jasmine.any(String));
  });

  it('no elimina si se cancela', () => {
    avisos.confirmar.and.returnValue(of(false));

    componente.eliminar();

    expect(servicio.eliminar).not.toHaveBeenCalled();
  });

  it('si el documento no existe muestra el error y ofrece volver', () => {
    servicio.obtener.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 404, error: { mensaje: 'El documento no existe.' } })),
    );

    componente.cargar();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('El documento no existe.');
    expect(fixture.nativeElement.textContent).toContain('Volver a mis documentos');
  });
});
