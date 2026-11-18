import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DocumentosService } from '../../core/documentos.service';
import { AvisosService } from '../../shared/avisos.service';
import { DocumentosComponent } from './documentos.component';

function lista(...archivos: File[]): File[] {
  return archivos;
}

const pdf = () => new File(['%PDF-1.4 contenido'], 'factura.pdf');

describe('DocumentosComponent', () => {
  let fixture: ComponentFixture<DocumentosComponent>;
  let componente: any;
  let servicio: jasmine.SpyObj<DocumentosService>;
  let avisos: jasmine.SpyObj<AvisosService>;

  beforeEach(async () => {
    servicio = jasmine.createSpyObj<DocumentosService>('DocumentosService', ['listar', 'obtenerSalud', 'procesar', 'eliminar']);
    servicio.listar.and.returnValue(of([]));
    servicio.obtenerSalud.and.returnValue(of({ estado: 'ok', modoDemostracion: false }));
    avisos = jasmine.createSpyObj<AvisosService>('AvisosService', ['exito', 'error', 'info', 'confirmar']);

    await TestBed.configureTestingModule({
      imports: [DocumentosComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: DocumentosService, useValue: servicio },
        { provide: AvisosService, useValue: avisos },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentosComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('muestra el estado vacío y aclara qué formatos no se aceptan', () => {
    const texto = fixture.nativeElement.textContent;

    expect(texto).toContain('Aún no tienes documentos');
    expect(texto).toContain('No se aceptan Excel, Word');
  });

  it('muestra la ayuda de formatos dentro del panel de subida', () => {
    componente.abrirPanel();
    fixture.detectChanges();

    const ayuda = fixture.nativeElement.querySelector('#ayuda-archivo').textContent;

    expect(ayuda).toContain('PDF, PNG y JPG');
    expect(ayuda).toContain('No se aceptan Excel, Word, PowerPoint');
  });

  it('no sube nada si no se adjuntó un archivo y explica qué falta', () => {
    componente.abrirPanel();

    componente.analizar();

    expect(servicio.procesar).not.toHaveBeenCalled();
    expect(componente.errorArchivo()).toContain('Adjunta el documento');
  });

  it('rechaza un Excel y explica por qué', async () => {
    componente.abrirPanel();

    await componente.recibir(lista(new File(['x'], 'ventas.xlsx')));

    expect(componente.archivo()).toBeNull();
    expect(componente.errorArchivo()).toContain('Los archivos de Excel (.xlsx) no se pueden analizar');
    expect(componente.errorArchivo()).toContain('guárdalo como PDF');
  });

  it('rechaza un Word y un archivo sin extensión', async () => {
    await componente.recibir(lista(new File(['x'], 'carta.docx')));
    expect(componente.errorArchivo()).toContain('Word');

    await componente.recibir(lista(new File(['x'], 'sin_extension')));
    expect(componente.errorArchivo()).toContain('no tiene un formato permitido');
  });

  it('rechaza un archivo cuyo contenido no es lo que dice su extensión', async () => {
    await componente.recibir(lista(new File(['PK-esto-es-un-excel'], 'falso.pdf')));

    expect(componente.archivo()).toBeNull();
    expect(componente.errorArchivo()).toContain('no corresponde a un archivo .pdf real');
  });

  it('rechaza varios archivos a la vez', async () => {
    await componente.recibir(lista(pdf(), pdf()));

    expect(componente.archivo()).toBeNull();
    expect(componente.errorArchivo()).toContain('un documento a la vez');
  });

  it('rechaza un archivo vacío y uno de más de 5 MB', async () => {
    await componente.recibir(lista(new File([], 'vacio.pdf')));
    expect(componente.errorArchivo()).toContain('vacío');

    await componente.recibir(lista(new File([new Uint8Array(6 * 1024 * 1024)], 'grande.pdf')));
    expect(componente.errorArchivo()).toContain('máximo es 5 MB');
  });

  it('acepta un PDF válido, lo deja a la vista y quita el error anterior', async () => {
    await componente.recibir(lista(new File(['x'], 'ventas.xlsx')));
    await componente.recibir(lista(pdf()));

    expect(componente.archivo()?.name).toBe('factura.pdf');
    expect(componente.errorArchivo()).toBeNull();
  });

  it('al analizar con éxito avisa en el centro y abre el documento', async () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    servicio.procesar.and.returnValue(of({ codDocumento: 7 } as any));
    await componente.recibir(lista(pdf()));

    componente.analizar();

    expect(servicio.procesar).toHaveBeenCalled();
    expect(avisos.exito).toHaveBeenCalledWith('Documento analizado', jasmine.any(String));
    expect(router.navigate).toHaveBeenCalledWith(['/documentos', 7]);
  });

  it('si el servidor rechaza el archivo muestra el motivo en un aviso central y deja reintentar', async () => {
    servicio.procesar.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { mensaje: 'Formato no permitido.' } })),
    );
    await componente.recibir(lista(pdf()));

    componente.analizar();

    expect(avisos.error).toHaveBeenCalledWith('No se pudo analizar el documento', 'Formato no permitido.');
    expect(componente.subiendo()).toBeFalse();
    expect(componente.archivo()).not.toBeNull();
  });

  it('pide confirmación antes de eliminar y no elimina si se cancela', () => {
    avisos.confirmar.and.returnValue(of(false));

    componente.eliminar({ codDocumento: 3, nombreArchivo: 'a.pdf' });

    expect(avisos.confirmar).toHaveBeenCalled();
    expect(servicio.eliminar).not.toHaveBeenCalled();
  });

  it('elimina, recarga y avisa cuando se confirma', () => {
    avisos.confirmar.and.returnValue(of(true));
    servicio.eliminar.and.returnValue(of(undefined));

    componente.eliminar({ codDocumento: 3, nombreArchivo: 'a.pdf' });

    expect(servicio.eliminar).toHaveBeenCalledWith(3);
    expect(servicio.listar).toHaveBeenCalledTimes(2);
    expect(avisos.exito).toHaveBeenCalledWith('Documento eliminado', jasmine.any(String));
  });

  it('pide confirmación al cerrar el panel con un archivo elegido', async () => {
    avisos.confirmar.and.returnValue(of(false));
    componente.abrirPanel();
    await componente.recibir(lista(pdf()));

    componente.cerrarPanel();

    expect(avisos.confirmar).toHaveBeenCalled();
    expect(componente.panelAbierto()).toBeTrue();
  });

  it('cierra el panel sin preguntar si no hay archivo', () => {
    componente.abrirPanel();

    componente.cerrarPanel();

    expect(avisos.confirmar).not.toHaveBeenCalled();
    expect(componente.panelAbierto()).toBeFalse();
  });
});
