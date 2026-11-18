/*
    DocuIA - pruebas de los procedimientos almacenados
    Base de datos: docuia

    Ejecuta cada procedimiento con casos correctos y con errores.
    Si una verificacion falla se lanza un error con el nombre de la prueba.
    Los datos de prueba usan el prefijo 'zz_prueba' y se eliminan al terminar.
*/
set nocount on;
set dateformat dmy;

declare @sql_limpia nvarchar(max) = N'
    delete m from dbo.ta_dia_docto_mnsje m inner join dbo.ta_dia_docto d on d.cod_docto = m.cod_docto
        inner join dbo.ta_dia_mst_mov_usrio u on u.cod_usrio = d.cod_usrio where u.nom_usrio like ''zz_prueba%'';
    delete c from dbo.ta_dia_docto_campo c inner join dbo.ta_dia_docto d on d.cod_docto = c.cod_docto
        inner join dbo.ta_dia_mst_mov_usrio u on u.cod_usrio = d.cod_usrio where u.nom_usrio like ''zz_prueba%'';
    delete n from dbo.ta_dia_docto_cntnd n inner join dbo.ta_dia_docto d on d.cod_docto = n.cod_docto
        inner join dbo.ta_dia_mst_mov_usrio u on u.cod_usrio = d.cod_usrio where u.nom_usrio like ''zz_prueba%'';
    delete d from dbo.ta_dia_docto d inner join dbo.ta_dia_mst_mov_usrio u on u.cod_usrio = d.cod_usrio
        where u.nom_usrio like ''zz_prueba%'';
    delete from dbo.ta_dia_mst_mov_usrio where nom_usrio like ''zz_prueba%'';';

exec sp_executesql @sql_limpia;

declare @r table (cod_resultado int, dsc_mensaje varchar(500));
declare @u table (cod_usrio int, nom_usrio varchar(100), nom_email varchar(150), nom_clave_hsh varchar(200), flg_ivtdo char(1), flg_actvo char(1));

declare @cod_ana int, @cod_beto int, @cod_ivtdo int, @cod_docto int, @cod_otro int, @cod_mnsje int;
declare @n int, @i int, @cod int;
declare @hash char(64) = replicate('a', 64);
declare @tbl dbo.tt_dia_campo;
declare @cnf dbo.tt_dia_campo_cnfrm;

begin try

    -- usuarios -----------------------------------------------------------------
    insert into @r exec dbo.pa_dia_aggar_usrio 'zz_prueba ana', 'zz_ana@prueba.com', 'hash-ana', 'N';
    select @cod_ana = cod_resultado from @r; delete from @r;
    if @cod_ana < 1 throw 50001, 'aggar_usrio: no registró al usuario', 1;

    insert into @r exec dbo.pa_dia_aggar_usrio 'zz_prueba otra', 'ZZ_ANA@prueba.com', 'hash', 'N';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_usrio: permitió un correo repetido (sin distinguir mayúsculas)', 1;

    insert into @r exec dbo.pa_dia_aggar_usrio 'zz_prueba sin clave', 'zz_x@prueba.com', null, 'N';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_usrio: permitió un usuario normal sin clave', 1;

    insert into @r exec dbo.pa_dia_aggar_usrio 'zz_prueba invitado', null, null, 'S';
    select @cod_ivtdo = cod_resultado from @r; delete from @r;
    if @cod_ivtdo < 1 throw 50001, 'aggar_usrio: no registró al invitado', 1;

    insert into @r exec dbo.pa_dia_aggar_usrio 'zz_prueba beto', 'zz_beto@prueba.com', 'hash-beto', 'N';
    select @cod_beto = cod_resultado from @r; delete from @r;

    insert into @u exec dbo.pa_dia_sel_usrio @nom_email = 'ZZ_Ana@prueba.com';
    select @n = count(*) from @u where cod_usrio = @cod_ana and nom_clave_hsh = 'hash-ana' and flg_ivtdo = 'N';
    if @n <> 1 throw 50001, 'sel_usrio: no encontró al usuario por correo', 1;
    delete from @u;

    insert into @u exec dbo.pa_dia_sel_usrio @cod_usrio = 999999;
    select @n = count(*) from @u;
    if @n <> 0 throw 50001, 'sel_usrio: devolvió un usuario inexistente', 1;

    insert into @r exec dbo.pa_dia_edtar_usrio @cod_ana;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> 1 or not exists (select 1 from dbo.ta_dia_mst_mov_usrio where cod_usrio = @cod_ana and fec_ultmo_login is not null)
        throw 50001, 'edtar_usrio: no guardó el último ingreso', 1;

    insert into @r exec dbo.pa_dia_edtar_usrio 999999;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'edtar_usrio: aceptó un usuario inexistente', 1;

    -- maestra ------------------------------------------------------------------
    declare @t table (cod_tpo_docto int, nom_tpo_docto varchar(50), dsc_tpo_docto varchar(200));
    insert into @t exec dbo.pa_dia_lst_tpo_docto;
    select @n = count(*) from @t;
    if @n < 4 throw 50001, 'lst_tpo_docto: faltan tipos de documento', 1;

    -- documentos ---------------------------------------------------------------
    insert into @r exec dbo.pa_dia_aggar_docto @cod_ana, 'factura.pdf', 'application/pdf', 5, @hash, 0x255044462D;
    select @cod_docto = cod_resultado from @r; delete from @r;
    if @cod_docto < 1 throw 50001, 'aggar_docto: no registró el documento', 1;

    if not exists (select 1 from dbo.ta_dia_docto_cntnd where cod_docto = @cod_docto and datalength(bin_cntnd) = 5)
        throw 50001, 'aggar_docto: no guardó el contenido', 1;

    insert into @r exec dbo.pa_dia_aggar_docto 999999, 'x.pdf', 'application/pdf', 5, @hash, 0x255044462D;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_docto: aceptó un usuario inexistente', 1;

    -- límite de 20 documentos por usuario
    set @i = 1;
    while @i < 20
    begin
        insert into @r exec dbo.pa_dia_aggar_docto @cod_ivtdo, 'relleno.pdf', 'application/pdf', 5, @hash, 0x255044462D;
        delete from @r;
        set @i += 1;
    end;
    insert into @r exec dbo.pa_dia_aggar_docto @cod_ivtdo, 'vigesimo.pdf', 'application/pdf', 5, @hash, 0x255044462D;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod < 1 throw 50001, 'aggar_docto: no permitió el documento número 20', 1;
    insert into @r exec dbo.pa_dia_aggar_docto @cod_ivtdo, 'sobra.pdf', 'application/pdf', 5, @hash, 0x255044462D;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_docto: no aplicó el máximo de 20 documentos', 1;

    insert into @r exec dbo.pa_dia_edtar_docto @cod_ana, @cod_docto, 1, 2, 'Factura de prueba';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> 1 throw 50001, 'edtar_docto: no actualizó el documento', 1;

    insert into @r exec dbo.pa_dia_edtar_docto @cod_ana, @cod_docto, 99, 2, 'x';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'edtar_docto: aceptó un tipo inexistente', 1;

    insert into @r exec dbo.pa_dia_edtar_docto @cod_ana, @cod_docto, 1, 99, 'x';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'edtar_docto: aceptó un estado inexistente', 1;

    insert into @r exec dbo.pa_dia_edtar_docto @cod_beto, @cod_docto, 1, 2, 'x';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'edtar_docto: permitió editar el documento de otro usuario', 1;

    -- campos -------------------------------------------------------------------
    insert into @tbl values (1, 'ruc', 'RUC', '20123456789', 0.95), (2, 'total', 'Total', '1180.00', 0.9), (3, 'fecha', 'Fecha', '15/10/2026', 0.8);
    insert into @r exec dbo.pa_dia_aggar_campo @cod_ana, @cod_docto, @tbl;
    select @cod = cod_resultado from @r; delete from @r;
    select @n = count(*) from dbo.ta_dia_docto_campo where cod_docto = @cod_docto;
    if @cod <> 1 or @n <> 3 throw 50001, 'aggar_campo: no guardó los 3 campos', 1;

    delete from @tbl;
    insert into @tbl values (1, 'ruc', 'RUC', '20999999999', 0.99), (2, 'total', 'Total', '500.00', 0.7);
    insert into @r exec dbo.pa_dia_aggar_campo @cod_ana, @cod_docto, @tbl;
    delete from @r;
    select @n = count(*) from dbo.ta_dia_docto_campo where cod_docto = @cod_docto;
    if @n <> 2 throw 50001, 'aggar_campo: no reemplazó los campos anteriores', 1;

    insert into @r exec dbo.pa_dia_aggar_campo @cod_beto, @cod_docto, @tbl;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_campo: permitió guardar en un documento ajeno', 1;

    insert into @cnf values (2, '505.00');
    insert into @r exec dbo.pa_dia_edtar_campo @cod_ana, @cod_docto, @cnf;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> 1 or not exists (select 1 from dbo.ta_dia_docto_campo where cod_docto = @cod_docto and cod_campo = 2 and dsc_valor_cnfrm = '505.00')
        throw 50001, 'edtar_campo: no guardó el valor confirmado', 1;

    insert into @r exec dbo.pa_dia_edtar_campo @cod_beto, @cod_docto, @cnf;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'edtar_campo: permitió confirmar en un documento ajeno', 1;

    -- consultas ----------------------------------------------------------------
    declare @l table (cod_docto int, nom_archv varchar(255), cod_tpo_docto int, nom_tpo_docto varchar(50),
                      cod_estdo_docto int, nom_estdo_docto varchar(50), num_tamno int, fec_rgtro varchar(10), hor_rgtro varchar(5));
    insert into @l exec dbo.pa_dia_lst_docto @cod_ana;
    select @n = count(*) from @l
    where cod_docto = @cod_docto and nom_tpo_docto = 'Factura' and nom_estdo_docto = 'Procesado'
      and fec_rgtro like '[0-3][0-9]/[0-1][0-9]/[0-9][0-9][0-9][0-9]' and hor_rgtro like '[0-2][0-9]:[0-5][0-9]';
    if @n <> 1 throw 50001, 'lst_docto: el listado no trae el documento con tipo, estado y fecha en dd/mm/yyyy', 1;

    declare @c table (nom_archv varchar(255), tpo_cntnd varchar(50), bin_cntnd varbinary(max));
    insert into @c exec dbo.pa_dia_sel_docto_cntnd @cod_ana, @cod_docto;
    select @n = count(*) from @c where datalength(bin_cntnd) = 5 and tpo_cntnd = 'application/pdf';
    if @n <> 1 throw 50001, 'sel_docto_cntnd: no devolvió el archivo', 1;
    delete from @c;
    insert into @c exec dbo.pa_dia_sel_docto_cntnd @cod_beto, @cod_docto;
    select @n = count(*) from @c;
    if @n <> 0 throw 50001, 'sel_docto_cntnd: entregó el archivo de otro usuario', 1;

    -- mensajes -----------------------------------------------------------------
    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_ana, @cod_docto, 1, '¿Cuál es el total?';
    select @cod_mnsje = cod_resultado from @r; delete from @r;
    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_ana, @cod_docto, 2, 'El total es 505.00';
    delete from @r;
    if @cod_mnsje < 1 throw 50001, 'aggar_mnsje: no guardó el mensaje', 1;

    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_ana, @cod_docto, 1, '   ';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_mnsje: aceptó un mensaje vacío', 1;

    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_ana, @cod_docto, 9, 'hola';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_mnsje: aceptó un origen inexistente', 1;

    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_beto, @cod_docto, 1, 'hola';
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'aggar_mnsje: permitió escribir en un documento ajeno', 1;

    declare @m table (cod_mnsje int, cod_orgen_mnsje int, nom_orgen_mnsje varchar(50), dsc_texto varchar(max), fec_rgtro varchar(10), hor_rgtro varchar(5));
    insert into @m exec dbo.pa_dia_lst_mnsje @cod_ana, @cod_docto;
    select @n = count(*) from @m;
    if @n <> 2 or (select top 1 nom_orgen_mnsje from @m order by cod_mnsje) <> 'Usuario'
        throw 50001, 'lst_mnsje: la conversación no está completa o en orden', 1;
    delete from @m;
    insert into @m exec dbo.pa_dia_lst_mnsje @cod_beto, @cod_docto;
    select @n = count(*) from @m;
    if @n <> 0 throw 50001, 'lst_mnsje: mostró mensajes de otro usuario', 1;

    -- eliminar -----------------------------------------------------------------
    insert into @r exec dbo.pa_dia_del_docto @cod_beto, @cod_docto;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'del_docto: permitió eliminar el documento de otro usuario', 1;

    insert into @r exec dbo.pa_dia_aggar_docto @cod_ana, 'borrar.pdf', 'application/pdf', 5, @hash, 0x255044462D;
    select @cod_otro = cod_resultado from @r; delete from @r;
    insert into @r exec dbo.pa_dia_aggar_mnsje @cod_ana, @cod_otro, 1, 'hola';
    delete from @r;
    insert into @r exec dbo.pa_dia_del_docto @cod_ana, @cod_otro;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> 1
       or exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_otro)
       or exists (select 1 from dbo.ta_dia_docto_cntnd where cod_docto = @cod_otro)
       or exists (select 1 from dbo.ta_dia_docto_mnsje where cod_docto = @cod_otro)
        throw 50001, 'del_docto: no eliminó el documento con sus datos', 1;

    -- trabajo de limpieza ------------------------------------------------------
    update dbo.ta_dia_docto set cnr_fec_rgtro = dateadd(day, -10, getdate()) where cod_usrio = @cod_ivtdo;
    update dbo.ta_dia_mst_mov_usrio set cnr_fec_rgtro = dateadd(day, -10, getdate()) where cod_usrio = @cod_ivtdo;

    insert into @r exec dbo.pa_dia_job_elmnr_docto 0;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> -1 throw 50001, 'job_elmnr_docto: aceptó 0 días', 1;

    insert into @r exec dbo.pa_dia_job_elmnr_docto 7;
    select @cod = cod_resultado from @r; delete from @r;
    if @cod <> 1 throw 50001, 'job_elmnr_docto: falló la limpieza', 1;
    if exists (select 1 from dbo.ta_dia_docto where cod_usrio = @cod_ivtdo)
       or exists (select 1 from dbo.ta_dia_mst_mov_usrio where cod_usrio = @cod_ivtdo)
        throw 50001, 'job_elmnr_docto: no eliminó los documentos ni el invitado antiguos', 1;
    if not exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_docto)
        throw 50001, 'job_elmnr_docto: eliminó un documento reciente', 1;

    exec sp_executesql @sql_limpia;
    select 'TODAS LAS PRUEBAS PASARON' as resultado;
end try
begin catch
    exec sp_executesql @sql_limpia;
    throw;
end catch
go
