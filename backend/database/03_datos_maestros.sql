/*
    DocuIA - datos iniciales de las tablas maestras.
    Cada insert verifica si el valor ya existe, asi que se puede repetir sin duplicar.
    cnr_usr_rgtro = 0 identifica al sistema.
*/

if not exists (select 1 from dbo.ta_dia_mst_estdo_docto where cod_estdo_docto = 1)
    insert into dbo.ta_dia_mst_estdo_docto (cod_estdo_docto, nom_estdo_docto, dsc_estdo_docto)
    values (1, 'Pendiente', 'El archivo se recibió y aún no se analiza');

if not exists (select 1 from dbo.ta_dia_mst_estdo_docto where cod_estdo_docto = 2)
    insert into dbo.ta_dia_mst_estdo_docto (cod_estdo_docto, nom_estdo_docto, dsc_estdo_docto)
    values (2, 'Procesado', 'La IA extrajo los datos del documento');

if not exists (select 1 from dbo.ta_dia_mst_estdo_docto where cod_estdo_docto = 3)
    insert into dbo.ta_dia_mst_estdo_docto (cod_estdo_docto, nom_estdo_docto, dsc_estdo_docto)
    values (3, 'Con error', 'No se pudo analizar el documento');
go

if not exists (select 1 from dbo.ta_dia_mst_orgen_mnsje where cod_orgen_mnsje = 1)
    insert into dbo.ta_dia_mst_orgen_mnsje (cod_orgen_mnsje, nom_orgen_mnsje, dsc_orgen_mnsje)
    values (1, 'Usuario', 'Pregunta escrita por la persona');

if not exists (select 1 from dbo.ta_dia_mst_orgen_mnsje where cod_orgen_mnsje = 2)
    insert into dbo.ta_dia_mst_orgen_mnsje (cod_orgen_mnsje, nom_orgen_mnsje, dsc_orgen_mnsje)
    values (2, 'Asistente', 'Respuesta generada por la IA');
go

if not exists (select 1 from dbo.ta_dia_mst_mov_tpo_docto where cod_tpo_docto = 1)
    insert into dbo.ta_dia_mst_mov_tpo_docto
        (cod_tpo_docto, nom_tpo_docto, dsc_tpo_docto, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
    values (1, 'Factura', 'Comprobante de venta o de pago', getdate(), 0, 1);

if not exists (select 1 from dbo.ta_dia_mst_mov_tpo_docto where cod_tpo_docto = 2)
    insert into dbo.ta_dia_mst_mov_tpo_docto
        (cod_tpo_docto, nom_tpo_docto, dsc_tpo_docto, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
    values (2, 'Contrato', 'Acuerdo entre dos o más partes', getdate(), 0, 1);

if not exists (select 1 from dbo.ta_dia_mst_mov_tpo_docto where cod_tpo_docto = 3)
    insert into dbo.ta_dia_mst_mov_tpo_docto
        (cod_tpo_docto, nom_tpo_docto, dsc_tpo_docto, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
    values (3, 'Constancia', 'Certificado o constancia emitida por una entidad', getdate(), 0, 1);

if not exists (select 1 from dbo.ta_dia_mst_mov_tpo_docto where cod_tpo_docto = 4)
    insert into dbo.ta_dia_mst_mov_tpo_docto
        (cod_tpo_docto, nom_tpo_docto, dsc_tpo_docto, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
    values (4, 'Otro', 'Documento que no encaja en los demás tipos', getdate(), 0, 1);
go
