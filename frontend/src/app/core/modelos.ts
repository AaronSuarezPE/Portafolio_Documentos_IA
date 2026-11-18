export interface RespuestaSesion {
  token: string;
  nombre: string;
  esInvitado: boolean;
  minutosVigencia: number;
}

export interface Sesion {
  token: string;
  nombre: string;
  esInvitado: boolean;
  expira: number;
}

export interface Salud {
  estado: string;
  modoDemostracion: boolean;
}

export interface DocumentoResumen {
  codDocumento: number;
  nombreArchivo: string;
  tipo: string;
  estado: string;
  tamanoBytes: number;
  fecha: string;
  hora: string;
}

export interface Campo {
  codCampo: number;
  clave: string;
  etiqueta: string;
  valor: string;
  valorConfirmado: string | null;
  confianza: number;
}

export interface Mensaje {
  codMensaje: number;
  origen: 'Usuario' | 'Asistente';
  texto: string;
  fecha: string;
  hora: string;
}

export interface DocumentoDetalle extends DocumentoResumen {
  resumen: string;
  campos: Campo[];
  mensajes: Mensaje[];
}

export interface CampoConfirmado {
  codCampo: number;
  valor: string;
}
