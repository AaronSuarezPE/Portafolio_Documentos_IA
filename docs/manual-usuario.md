# DocuIA · Manual de usuario

DocuIA lee tus documentos y te entrega sus datos ordenados. Tú siempre tienes la última palabra: puedes corregir lo que la IA leyó antes de confirmarlo.

## Cómo funcionan los mensajes

- **Avisos y confirmaciones** aparecen en un cuadro **en el centro de la pantalla**, con un ícono: ✓ verde (listo), ! rojo (algo falló), i azul (información) y ? amarillo (te pide confirmar). Se cierran con **Entendido**, con la tecla **Esc** o, en las preguntas, con **Cancelar**.
- **Formularios nuevos** (por ejemplo, subir un documento) se abren en un **panel que entra por la derecha**. Se cierra con la **×**, con **Esc**, con un clic en el fondo o con **Cancelar**.
- **Errores en un campo** aparecen en rojo justo debajo del campo y el cursor se coloca en el primero que debes corregir. Los campos con asterisco rojo (*) son obligatorios.

## 1. Qué archivos se pueden subir

| Se acepta | No se acepta |
|---|---|
| **PDF** (`.pdf`) | Excel (`.xls`, `.xlsx`, `.csv`) |
| **Imágenes PNG** (`.png`) | Word (`.doc`, `.docx`) |
| **Imágenes JPG** (`.jpg`, `.jpeg`) | PowerPoint, texto (`.txt`), comprimidos (`.zip`, `.rar`) y cualquier otro formato |

Además:

- El archivo debe pesar **hasta 5 MB** y **no estar vacío**.
- Se sube **un documento a la vez**.
- Aunque cambies la extensión de un archivo (por ejemplo, llamar `.pdf` a un Excel), se rechaza: DocuIA revisa el contenido real.
- Si tu documento está en Excel o Word, **guárdalo o expórtalo como PDF** y súbelo de nuevo.

La ayuda «Qué puedes subir» aparece dentro del panel de subida, y cada rechazo te dice el motivo y qué hacer.

## 2. Ingresar

Al abrir la aplicación verás tres pestañas.

- **Ingresar:** correo y clave, ambos obligatorios. Si el correo no tiene un formato válido o falta un dato, se marca el campo. Si la clave es incorrecta verás «El correo o la clave no son correctos» (por seguridad no se dice cuál de los dos falló).
- **Crear cuenta:** nombre (2 a 100 caracteres), correo válido y una clave de **al menos 8 caracteres**, que debes repetir. Si el correo ya está registrado, te lo indica.
- **Invitado:** entra sin cuenta para probar. Es una sesión temporal; sus documentos se borran solos.

Si haces demasiados intentos seguidos, espera un minuto. Puedes ver u ocultar la clave con el botón **Ver** / **Ocultar** del campo.

## 3. Mis documentos

Es la pantalla principal. Muestra una tarjeta por documento con su nombre, tipo, tamaño, fecha y estado:

- **Pendiente:** se está analizando.
- **Procesado:** listo para revisar.
- **Con error:** no se pudo leer; puedes eliminarlo y subirlo de nuevo.

Si ves el aviso amarillo de **modo demostración**, la IA real no está activada y los datos mostrados son de ejemplo. Cuando no tienes documentos, la pantalla te lo dice y te recuerda los formatos permitidos.

Puedes guardar **hasta 20 documentos a la vez**. Para subir otro, elimina alguno. Los documentos se borran automáticamente a los 7 días.

## 4. Subir y analizar un documento

1. Pulsa **Subir documento**: se abre el panel por la derecha.
2. Arrastra el archivo al recuadro o pulsa **Elegir archivo**. El archivo elegido aparece con su nombre y tamaño; puedes **Quitarlo** y elegir otro.
3. Pulsa **Analizar documento**. Si no elegiste archivo, se te avisa que debes adjuntarlo.
4. Mientras la IA trabaja verás una barra de progreso (puede tardar hasta un minuto) y no se puede cerrar el panel.
5. Al terminar aparece el aviso **«Documento analizado»** y se abre el detalle.

**Si algo sale mal** el panel se mantiene abierto con el archivo elegido y un aviso central explica el motivo (archivo no permitido, sin conexión, servidor ocupado, límite de documentos o de solicitudes). Si cierras el panel con un archivo elegido, se te pregunta si quieres salir sin analizar.

## 5. Revisar y confirmar los datos

Al abrir un documento verás el **resumen**, y a la izquierda los **datos encontrados** (emisor, número, fecha, importes…).

- Los datos marcados **Revisar · NN %** tienen baja confianza: compáralos con el original.
- Cada campo es editable. Todos son obligatorios y admiten hasta 1000 caracteres.
- Pulsa **Confirmar datos**. Si hay un campo vacío o con solo espacios, no se guarda: el campo se marca y el cursor va al primero.
- Al guardar aparece **«Datos confirmados»**. Debajo de cada campo corregido se muestra lo que había leído la IA («La IA leyó: …»). Los valores confirmados se conservan cuando vuelves a abrir el documento.

## 6. Preguntarle al documento

A la derecha está el chat. Escribe tu pregunta (hasta 500 caracteres; **Enter** envía, **Mayús + Enter** baja de línea), por ejemplo «¿Cuál es el total?». La respuesta se basa solo en ese documento. La conversación queda guardada junto al documento.

- Una pregunta vacía no se envía y el campo se marca.
- Si el envío falla, tu pregunta vuelve a la caja para que reintentes y se explica el motivo.

## 7. Eliminar un documento

Usa **Eliminar** en la lista o dentro del documento. Se pide confirmación («¿Eliminar el documento?», con el botón rojo) porque borra el documento, sus datos y su conversación y **no se puede deshacer**. Luego aparece **«Documento eliminado»**.

## 8. Sesión

**Salir** (arriba a la derecha) cierra tu sesión. Si la sesión vence mientras trabajas, aparece **«Tu sesión venció»** y te lleva a ingresar de nuevo. Cada persona ve solo sus propios documentos.

## 9. Problemas frecuentes

| Qué ves | Qué significa | Qué hacer |
|---|---|---|
| «Los archivos de Excel (.xlsx) no se pueden analizar» | Formato no permitido | Guárdalo como PDF y súbelo de nuevo |
| «El contenido de … no corresponde a un archivo .pdf real» | El archivo cambió de extensión | Usa el archivo original o expórtalo a PDF |
| «El archivo pesa … y el máximo es 5 MB» | Archivo demasiado grande | Reduce o comprime la imagen/PDF |
| «El archivo está vacío» | Archivo de 0 bytes | Elige el archivo correcto |
| «Solo se puede analizar un documento a la vez» | Soltaste varios archivos | Elige uno |
| «No se pudo conectar con el servidor» | Sin internet o servidor apagado | Revisa tu conexión y reintenta |
| «Hiciste demasiadas solicitudes» | Límite por minuto | Espera un minuto |
| «Llegaste al máximo de 20 documentos» | Límite alcanzado | Elimina alguno |
