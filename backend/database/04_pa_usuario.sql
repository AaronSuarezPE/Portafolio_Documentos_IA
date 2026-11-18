/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_aggar_usrio
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Registra un usuario. Si es invitado no necesita correo ni clave.

    Ejemplo:
      exec dbo.pa_dia_aggar_usrio @nom_usrio = 'Ana', @nom_email = 'ana@correo.com', @nom_clave_hsh = 'hash', @flg_ivtdo = 'N';
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_aggar_usrio
    @nom_usrio     varchar(100),
    @nom_email     varchar(150) = null,
    @nom_clave_hsh varchar(200) = null,
    @flg_ivtdo     char(1) = 'N'
as
begin
    set nocount on;
    set dateformat dmy;

    declare @cod_usrio int;
    declare @dsc_mensaje varchar(200);

    begin try
        set @nom_usrio = ltrim(rtrim(isnull(@nom_usrio, '')));
        set @nom_email = lower(ltrim(rtrim(@nom_email)));

        if @nom_usrio = ''
        begin
            select -1 as cod_resultado, 'Indica el nombre del usuario' as dsc_mensaje;
            return;
        end;

        if @flg_ivtdo not in ('S', 'N')
        begin
            select -1 as cod_resultado, 'El indicador de invitado no es válido' as dsc_mensaje;
            return;
        end;

        if @flg_ivtdo = 'N' and (isnull(@nom_email, '') = '' or isnull(@nom_clave_hsh, '') = '')
        begin
            select -1 as cod_resultado, 'El correo y la clave son obligatorios' as dsc_mensaje;
            return;
        end;

        begin transaction;

        if @nom_email is not null
           and exists (select 1 from dbo.ta_dia_mst_mov_usrio with (updlock, holdlock) where nom_email = @nom_email)
        begin
            set @cod_usrio = -1;
            set @dsc_mensaje = 'El correo ya está registrado';
        end
        else
        begin
            select @cod_usrio = isnull(max(cod_usrio), 0) + 1
            from dbo.ta_dia_mst_mov_usrio with (updlock, holdlock);

            insert into dbo.ta_dia_mst_mov_usrio
                (cod_usrio, nom_usrio, nom_email, nom_clave_hsh, flg_ivtdo, flg_actvo,
                 cnr_fec_rgtro, cnr_usr_rgtro, cnr_estdo_rgtro)
            values
                (@cod_usrio, @nom_usrio, @nom_email, @nom_clave_hsh, @flg_ivtdo, 'S',
                 getdate(), 0, 1);

            set @dsc_mensaje = 'Usuario registrado';
        end;

        commit transaction;

        select @cod_usrio as cod_resultado, @dsc_mensaje as dsc_mensaje;
    end try
    begin catch
        if @@trancount > 0 rollback transaction;
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_sel_usrio
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Devuelve un usuario activo, buscado por codigo o por correo.

    Ejemplo:
      exec dbo.pa_dia_sel_usrio @nom_email = 'ana@correo.com';
      -- Retorna un registro, o ninguno si no existe
*/
create or alter procedure dbo.pa_dia_sel_usrio
    @cod_usrio int = null,
    @nom_email varchar(150) = null
as
begin
    set nocount on;
    set dateformat dmy;

    set @nom_email = lower(ltrim(rtrim(@nom_email)));

    select
        u.cod_usrio,
        u.nom_usrio,
        u.nom_email,
        u.nom_clave_hsh,
        u.flg_ivtdo,
        u.flg_actvo
    from dbo.ta_dia_mst_mov_usrio u
    where u.cnr_estdo_rgtro = 1
      and u.flg_actvo = 'S'
      and ((@cod_usrio is not null and u.cod_usrio = @cod_usrio)
           or (@nom_email is not null and u.nom_email = @nom_email));
end;
go

/*
    Procedimiento Almacenado desarrollado por Aaron Suarez
    Nombre: pa_dia_edtar_usrio
    Autor: Aaron Suarez
    Fecha Creacion: 17/11/2026
    Fecha ultima actualizacion:
    Nombre ultima persona que actualizo:
    Base de datos: docuia

    Descripcion: Guarda la fecha del ultimo ingreso del usuario.

    Ejemplo:
      exec dbo.pa_dia_edtar_usrio @cod_usrio = 1;
      -- Retorna un SELECT con 'cod_resultado' y 'dsc_mensaje'
*/
create or alter procedure dbo.pa_dia_edtar_usrio
    @cod_usrio int
as
begin
    set nocount on;
    set dateformat dmy;

    begin try
        update dbo.ta_dia_mst_mov_usrio
        set fec_ultmo_login = getdate(),
            cnr_fec_mdfcn = getdate(),
            cnr_usr_mdfcn = @cod_usrio
        where cod_usrio = @cod_usrio
          and cnr_estdo_rgtro = 1;

        if @@rowcount = 0
        begin
            select -1 as cod_resultado, 'El usuario no existe' as dsc_mensaje;
            return;
        end;

        select 1 as cod_resultado, 'Ingreso registrado' as dsc_mensaje;
    end try
    begin catch
        select -1 as cod_resultado, error_message() as dsc_mensaje;
    end catch
end;
go
