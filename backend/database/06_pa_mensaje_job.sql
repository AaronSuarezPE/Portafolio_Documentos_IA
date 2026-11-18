/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_aggar_mnsje
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Guarda un mensaje de la conversacion sobre un documento.

    Ejemplo:
      exec dbo.pa_dia_aggar_mnsje @cod_usrio = 1, @cod_docto = 1, @cod_orgen_mnsje = 1, @dsc_texto = '¿Cuál es el total?';
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_aggar_mnsje
    @cod_usrio       int,
    @cod_docto       int,
    @cod_orgen_mnsje int,
    @dsc_texto       varchar(max)
as
begin
    set nocount on;
    set dateformat dmy;

    declare @cod_mnsje int;

    begin try
        if not exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_docto and cod_usrio = @cod_usrio)
        begin
            select -1 as cod_resultado, 'El documento no existe' as dsc_mensaje;
            return;
        end;

        if not exists (select 1 from dbo.ta_dia_mst_orgen_mnsje where cod_orgen_mnsje = @cod_orgen_mnsje)
        begin
            select -1 as cod_resultado, 'El origen del mensaje no existe' as dsc_mensaje;
            return;
        end;

        if isnull(ltrim(rtrim(@dsc_texto)), '') = ''
        begin
            select -1 as cod_resultado, 'El mensaje está vacío' as dsc_mensaje;
            return;
        end;

        begin transaction;

        select @cod_mnsje = isnull(max(cod_mnsje), 0) + 1
        from dbo.ta_dia_docto_mnsje with (updlock, holdlock);

        insert into dbo.ta_dia_docto_mnsje
            (cod_mnsje, cod_docto, cod_orgen_mnsje, dsc_texto, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
        values
            (@cod_mnsje, @cod_docto, @cod_orgen_mnsje, @dsc_texto, getdate(), @cod_usrio, 1);

        commit transaction;

        select @cod_mnsje as cod_resultado, 'Mensaje guardado' as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_lst_mnsje
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Lista la conversacion de un documento en orden cronologico.

    Ejemplo:
      exec dbo.pa_dia_lst_mnsje @cod_usrio = 1, @cod_docto = 1;
      -- Retorna un listado
*/
create or alter procedure dbo.pa_dia_lst_mnsje
    @cod_usrio int,
    @cod_docto int
as
begin
    set nocount on;
    set dateformat dmy;

    select
        m.cod_mnsje,
        m.cod_orgen_mnsje,
        o.nom_orgen_mnsje,
        m.dsc_texto,
        convert(varchar(10), m.cnr_fec_rgtro, 103) as fec_rgtro,
        left(convert(varchar(8), m.cnr_fec_rgtro, 108), 5) as hor_rgtro
    from dbo.ta_dia_docto_mnsje m
    inner join dbo.ta_dia_docto d on d.cod_docto = m.cod_docto
    inner join dbo.ta_dia_mst_orgen_mnsje o on o.cod_orgen_mnsje = m.cod_orgen_mnsje
    where m.cod_docto = @cod_docto
      and d.cod_usrio = @cod_usrio
    order by m.cod_mnsje;
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_job_elmnr_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Trabajo de limpieza. Elimina los documentos con mas de N dias y los
                 invitados antiguos que ya no tienen documentos.

    Ejemplo:
      exec dbo.pa_dia_job_elmnr_docto @num_dias = 7;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje' (cantidades en el mensaje)
*/
create or alter procedure dbo.pa_dia_job_elmnr_docto
    @num_dias int
as
begin
    set nocount on;

    declare @fec_corte datetime = dateadd(day, -@num_dias, getdate());
    declare @tbl_docto table (cod_docto int primary key);
    declare @num_docto int = 0;
    declare @num_ivtdo int = 0;

    begin try
        if @num_dias < 1
        begin
            select -1 as cod_resultado, 'Indica un número de días mayor a cero' as dsc_mensaje;
            return;
        end;

        begin transaction;

        insert into @tbl_docto (cod_docto)
        select cod_docto from dbo.ta_dia_docto where cnr_fec_rgtro < @fec_corte;

        delete m from dbo.ta_dia_docto_mnsje m inner join @tbl_docto t on t.cod_docto = m.cod_docto;
        delete c from dbo.ta_dia_docto_campo c inner join @tbl_docto t on t.cod_docto = c.cod_docto;
        delete n from dbo.ta_dia_docto_cntnd n inner join @tbl_docto t on t.cod_docto = n.cod_docto;
        delete d from dbo.ta_dia_docto d inner join @tbl_docto t on t.cod_docto = d.cod_docto;
        set @num_docto = @@rowcount;

        delete u
        from dbo.ta_dia_mst_mov_usrio u
        where u.flg_ivtdo = 'S'
          and u.cnr_fec_rgtro < @fec_corte
          and not exists (select 1 from dbo.ta_dia_docto d where d.cod_usrio = u.cod_usrio);
        set @num_ivtdo = @@rowcount;

        commit transaction;

        select 1 as cod_resultado,
               'Documentos eliminados: ' + cast(@num_docto as varchar(10))
               + '. Invitados eliminados: ' + cast(@num_ivtdo as varchar(10)) as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go
