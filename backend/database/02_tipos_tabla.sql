/*
    DocuIA - tipos tabla (TVP)
    Se usan porque la estructura que envia el backend es conocida: no hace falta json.
*/

if type_id('dbo.tt_dia_campo') is null
    create type dbo.tt_dia_campo as table (
        num_orden int           not null,
        nom_clave varchar(60)   not null,
        nom_etqta varchar(100)  not null,
        dsc_valor varchar(1000) not null,
        num_cnfnz decimal(4, 3) not null
    );
go

if type_id('dbo.tt_dia_campo_cnfrm') is null
    create type dbo.tt_dia_campo_cnfrm as table (
        cod_campo       int           not null,
        dsc_valor_cnfrm varchar(1000) not null
    );
go
