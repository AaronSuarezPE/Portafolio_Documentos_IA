/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_lst_tpo_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Lista los tipos de documento vigentes (para combos y para la IA).

    Ejemplo:
      exec dbo.pa_dia_lst_tpo_docto;
      -- Retorna un listado
*/
create or alter procedure dbo.pa_dia_lst_tpo_docto
as
begin
    set nocount on;

    select cod_tpo_docto, nom_tpo_docto, dsc_tpo_docto
    from dbo.ta_dia_mst_mov_tpo_docto
    where cnr_estdo_rgtro = 1
    order by cod_tpo_docto;
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_aggar_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Registra un documento subido por un usuario junto con su contenido.
                 Cada usuario puede tener hasta 20 documentos a la vez.

    Ejemplo:
      exec dbo.pa_dia_aggar_docto @cod_usrio = 1, @nom_archv = 'factura.pdf', @tpo_cntnd = 'application/pdf',
           @num_tamno = 1024, @nom_hsh = '...', @bin_cntnd = 0x255044462D;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_aggar_docto
    @cod_usrio int,
    @nom_archv varchar(255),
    @tpo_cntnd varchar(50),
    @num_tamno int,
    @nom_hsh   char(64),
    @bin_cntnd varbinary(max)
as
begin
    set nocount on;
    set dateformat dmy;

    declare @cod_docto int;
    declare @num_maxmo int = 20;

    begin try
        if not exists (select 1 from dbo.ta_dia_mst_mov_usrio where cod_usrio = @cod_usrio and flg_actvo = 'S' and cnr_estdo_rgtro = 1)
        begin
            select -1 as cod_resultado, 'El usuario no existe' as dsc_mensaje;
            return;
        end;

        if (select count(*) from dbo.ta_dia_docto where cod_usrio = @cod_usrio) >= @num_maxmo
        begin
            select -1 as cod_resultado, 'Llegaste al máximo de 20 documentos. Elimina alguno para continuar' as dsc_mensaje;
            return;
        end;

        begin transaction;

        select @cod_docto = isnull(max(cod_docto), 0) + 1
        from dbo.ta_dia_docto with (updlock, holdlock);

        insert into dbo.ta_dia_docto
            (cod_docto, cod_usrio, cod_tpo_docto, cod_estdo_docto, nom_archv, tpo_cntnd, num_tamno, nom_hsh,
             cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
        values
            (@cod_docto, @cod_usrio, null, 1, @nom_archv, @tpo_cntnd, @num_tamno, @nom_hsh,
             getdate(), @cod_usrio, 1);

        insert into dbo.ta_dia_docto_cntnd
            (cod_docto, bin_cntnd, cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
        values
            (@cod_docto, @bin_cntnd, getdate(), @cod_usrio, 1);

        commit transaction;

        select @cod_docto as cod_resultado, 'Documento registrado' as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_edtar_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Actualiza el estado, el tipo y el resumen de un documento del usuario.

    Ejemplo:
      exec dbo.pa_dia_edtar_docto @cod_usrio = 1, @cod_docto = 1, @cod_tpo_docto = 1, @cod_estdo_docto = 2, @dsc_rsmen = 'Factura de octubre';
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_edtar_docto
    @cod_usrio       int,
    @cod_docto       int,
    @cod_tpo_docto   int = null,
    @cod_estdo_docto int,
    @dsc_rsmen       varchar(1000) = null
as
begin
    set nocount on;
    set dateformat dmy;

    begin try
        if @cod_tpo_docto is not null
           and not exists (select 1 from dbo.ta_dia_mst_mov_tpo_docto where cod_tpo_docto = @cod_tpo_docto and cnr_estdo_rgtro = 1)
        begin
            select -1 as cod_resultado, 'El tipo de documento no existe' as dsc_mensaje;
            return;
        end;

        if not exists (select 1 from dbo.ta_dia_mst_estdo_docto where cod_estdo_docto = @cod_estdo_docto)
        begin
            select -1 as cod_resultado, 'El estado del documento no existe' as dsc_mensaje;
            return;
        end;

        update dbo.ta_dia_docto
        set cod_tpo_docto = @cod_tpo_docto,
            cod_estdo_docto = @cod_estdo_docto,
            dsc_rsmen = @dsc_rsmen,
            cnr_fec_mdfcn = getdate(),
            cnr_usr_mdfcn = @cod_usrio
        where cod_docto = @cod_docto
          and cod_usrio = @cod_usrio;

        if @@rowcount = 0
        begin
            select -1 as cod_resultado, 'El documento no existe' as dsc_mensaje;
            return;
        end;

        select 1 as cod_resultado, 'Documento actualizado' as dsc_mensaje;
    end try
    begin catch
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_aggar_campo
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Guarda los campos que la IA extrajo de un documento. Reemplaza los anteriores.

    Ejemplo:
      declare @tbl dbo.tt_dia_campo;
      insert into @tbl values (1, 'ruc', 'RUC', '20123456789', 0.95);
      exec dbo.pa_dia_aggar_campo @cod_usrio = 1, @cod_docto = 1, @tbl_campo = @tbl;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_aggar_campo
    @cod_usrio  int,
    @cod_docto  int,
    @tbl_campo  dbo.tt_dia_campo readonly
as
begin
    set nocount on;
    set dateformat dmy;

    begin try
        if not exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_docto and cod_usrio = @cod_usrio)
        begin
            select -1 as cod_resultado, 'El documento no existe' as dsc_mensaje;
            return;
        end;

        begin transaction;

        delete from dbo.ta_dia_docto_campo where cod_docto = @cod_docto;

        insert into dbo.ta_dia_docto_campo
            (cod_docto, cod_campo, nom_clave, nom_etqta, dsc_valor, num_cnfnz,
             cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
        select
            @cod_docto, t.num_orden, t.nom_clave, t.nom_etqta, t.dsc_valor, t.num_cnfnz,
            getdate(), @cod_usrio, 1
        from @tbl_campo t;

        commit transaction;

        select 1 as cod_resultado, 'Campos guardados' as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_edtar_campo
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Guarda los valores que la persona reviso y confirmo en el formulario.

    Ejemplo:
      declare @tbl dbo.tt_dia_campo_cnfrm;
      insert into @tbl values (1, '20123456789');
      exec dbo.pa_dia_edtar_campo @cod_usrio = 1, @cod_docto = 1, @tbl_campo = @tbl;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_edtar_campo
    @cod_usrio int,
    @cod_docto int,
    @tbl_campo dbo.tt_dia_campo_cnfrm readonly
as
begin
    set nocount on;
    set dateformat dmy;

    begin try
        if not exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_docto and cod_usrio = @cod_usrio)
        begin
            select -1 as cod_resultado, 'El documento no existe' as dsc_mensaje;
            return;
        end;

        update c
        set c.dsc_valor_cnfrm = t.dsc_valor_cnfrm,
            c.cnr_fec_mdfcn = getdate(),
            c.cnr_usr_mdfcn = @cod_usrio
        from dbo.ta_dia_docto_campo c
        inner join @tbl_campo t on t.cod_campo = c.cod_campo
        where c.cod_docto = @cod_docto;

        select 1 as cod_resultado, 'Valores confirmados' as dsc_mensaje;
    end try
    begin catch
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_lst_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Lista los documentos del usuario, del mas reciente al mas antiguo.

    Ejemplo:
      exec dbo.pa_dia_lst_docto @cod_usrio = 1;
      -- Retorna un listado
*/
create or alter procedure dbo.pa_dia_lst_docto
    @cod_usrio int
as
begin
    set nocount on;
    set dateformat dmy;

    select
        d.cod_docto,
        d.nom_archv,
        d.cod_tpo_docto,
        isnull(t.nom_tpo_docto, '') as nom_tpo_docto,
        d.cod_estdo_docto,
        e.nom_estdo_docto,
        d.num_tamno,
        convert(varchar(10), d.cnr_fec_rgtro, 103) as fec_rgtro,
        left(convert(varchar(8), d.cnr_fec_rgtro, 108), 5) as hor_rgtro
    from dbo.ta_dia_docto d
    inner join dbo.ta_dia_mst_estdo_docto e on e.cod_estdo_docto = d.cod_estdo_docto
    left join dbo.ta_dia_mst_mov_tpo_docto t on t.cod_tpo_docto = d.cod_tpo_docto
    where d.cod_usrio = @cod_usrio
    order by d.cnr_fec_rgtro desc, d.cod_docto desc;
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_sel_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Devuelve un documento del usuario y sus campos extraidos.

    Ejemplo:
      exec dbo.pa_dia_sel_docto @cod_usrio = 1, @cod_docto = 1;
      -- Retorna dos conjuntos: el documento y sus campos
*/
create or alter procedure dbo.pa_dia_sel_docto
    @cod_usrio int,
    @cod_docto int
as
begin
    set nocount on;
    set dateformat dmy;

    select
        d.cod_docto,
        d.nom_archv,
        d.cod_tpo_docto,
        isnull(t.nom_tpo_docto, '') as nom_tpo_docto,
        d.cod_estdo_docto,
        e.nom_estdo_docto,
        d.num_tamno,
        isnull(d.dsc_rsmen, '') as dsc_rsmen,
        convert(varchar(10), d.cnr_fec_rgtro, 103) as fec_rgtro,
        left(convert(varchar(8), d.cnr_fec_rgtro, 108), 5) as hor_rgtro
    from dbo.ta_dia_docto d
    inner join dbo.ta_dia_mst_estdo_docto e on e.cod_estdo_docto = d.cod_estdo_docto
    left join dbo.ta_dia_mst_mov_tpo_docto t on t.cod_tpo_docto = d.cod_tpo_docto
    where d.cod_docto = @cod_docto
      and d.cod_usrio = @cod_usrio;

    select
        c.cod_campo,
        c.nom_clave,
        c.nom_etqta,
        c.dsc_valor,
        c.dsc_valor_cnfrm,
        c.num_cnfnz
    from dbo.ta_dia_docto_campo c
    inner join dbo.ta_dia_docto d on d.cod_docto = c.cod_docto
    where c.cod_docto = @cod_docto
      and d.cod_usrio = @cod_usrio
    order by c.cod_campo;
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_sel_docto_cntnd
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Devuelve el archivo guardado de un documento del usuario.

    Ejemplo:
      exec dbo.pa_dia_sel_docto_cntnd @cod_usrio = 1, @cod_docto = 1;
      -- Retorna un registro, o ninguno si no existe
*/
create or alter procedure dbo.pa_dia_sel_docto_cntnd
    @cod_usrio int,
    @cod_docto int
as
begin
    set nocount on;

    select d.nom_archv, d.tpo_cntnd, c.bin_cntnd
    from dbo.ta_dia_docto d
    inner join dbo.ta_dia_docto_cntnd c on c.cod_docto = d.cod_docto
    where d.cod_docto = @cod_docto
      and d.cod_usrio = @cod_usrio;
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_del_docto
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Elimina un documento del usuario con su archivo, campos y conversacion.

    Ejemplo:
      exec dbo.pa_dia_del_docto @cod_usrio = 1, @cod_docto = 1;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_del_docto
    @cod_usrio int,
    @cod_docto int
as
begin
    set nocount on;

    begin try
        if not exists (select 1 from dbo.ta_dia_docto where cod_docto = @cod_docto and cod_usrio = @cod_usrio)
        begin
            select -1 as cod_resultado, 'El documento no existe' as dsc_mensaje;
            return;
        end;

        begin transaction;

        delete from dbo.ta_dia_docto_mnsje where cod_docto = @cod_docto;
        delete from dbo.ta_dia_docto_campo where cod_docto = @cod_docto;
        delete from dbo.ta_dia_docto_cntnd where cod_docto = @cod_docto;
        delete from dbo.ta_dia_docto where cod_docto = @cod_docto;

        commit transaction;

        select 1 as cod_resultado, 'Documento eliminado' as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go
