/*
    DocuIA - tablas
    Base de datos: docuia
    Fecha creacion: 17/11/2026

    Clasificacion (reglas de base de datos):
      mst_      maestras sin movimiento: valores fijos, sin auditoria.
      mst_mov_  maestras con movimiento: se administran y cambian, con auditoria cnr_.
      resto     operativas: con auditoria cnr_.

    El script se puede ejecutar varias veces: solo crea lo que falta.
*/

-- maestras sin movimiento -----------------------------------------------------

if object_id('dbo.ta_dia_mst_estdo_docto', 'u') is null
    create table dbo.ta_dia_mst_estdo_docto (
        cod_estdo_docto int          not null,
        nom_estdo_docto varchar(50)  not null,
        dsc_estdo_docto varchar(200) not null,
        constraint pk_ta_dia_mst_estdo_docto primary key (cod_estdo_docto)
    );
go

if object_id('dbo.ta_dia_mst_orgen_mnsje', 'u') is null
    create table dbo.ta_dia_mst_orgen_mnsje (
        cod_orgen_mnsje int          not null,
        nom_orgen_mnsje varchar(50)  not null,
        dsc_orgen_mnsje varchar(200) not null,
        constraint pk_ta_dia_mst_orgen_mnsje primary key (cod_orgen_mnsje)
    );
go

-- maestras con movimiento -----------------------------------------------------

if object_id('dbo.ta_dia_mst_mov_tpo_docto', 'u') is null
    create table dbo.ta_dia_mst_mov_tpo_docto (
        cod_tpo_docto   int          not null,
        nom_tpo_docto   varchar(50)  not null,
        dsc_tpo_docto   varchar(200) not null,
        cnr_fec_rgtro   datetime     not null,
        cnr_usr_rgtro   int          not null,
        cnr_fec_mdfcn   datetime     null,
        cnr_usr_mdfcn   int          null,
        cnr_estdo_rgtro int          not null,
        constraint pk_ta_dia_mst_mov_tpo_docto primary key (cod_tpo_docto)
    );
go

if object_id('dbo.ta_dia_mst_mov_usrio', 'u') is null
    create table dbo.ta_dia_mst_mov_usrio (
        cod_usrio       int          not null,
        nom_usrio       varchar(100) not null,
        nom_email       varchar(150) null,
        nom_clave_hsh   varchar(200) null,
        flg_ivtdo       char(1)      not null,
        flg_actvo       char(1)      not null,
        fec_ultmo_login datetime     null,
        cnr_fec_rgtro   datetime     not null,
        cnr_usr_rgtro   int          not null,
        cnr_fec_mdfcn   datetime     null,
        cnr_usr_mdfcn   int          null,
        cnr_estdo_rgtro int          not null,
        constraint pk_ta_dia_mst_mov_usrio primary key (cod_usrio),
        constraint ck_ta_dia_mst_mov_usrio_ivtdo check (flg_ivtdo in ('S', 'N')),
        constraint ck_ta_dia_mst_mov_usrio_actvo check (flg_actvo in ('S', 'N'))
    );
go

-- un correo no se repite; los invitados no tienen correo
if not exists (select 1 from sys.indexes where name = 'ux_ta_dia_mst_mov_usrio_email')
    create unique index ux_ta_dia_mst_mov_usrio_email
        on dbo.ta_dia_mst_mov_usrio (nom_email)
        where nom_email is not null;
go

-- operativas ------------------------------------------------------------------

if object_id('dbo.ta_dia_docto', 'u') is null
    create table dbo.ta_dia_docto (
        cod_docto       int          not null,
        cod_usrio       int          not null,
        cod_tpo_docto   int          null,
        cod_estdo_docto int          not null,
        nom_archv       varchar(255) not null,
        tpo_cntnd       varchar(50)  not null,
        num_tamno       int          not null,
        nom_hsh         char(64)     not null,
        dsc_rsmen       varchar(1000) null,
        cnr_fec_rgtro   datetime     not null,
        cnr_usr_rgtro   int          not null,
        cnr_fec_mdfcn   datetime     null,
        cnr_usr_mdfcn   int          null,
        cnr_estdo_rgtro int          not null,
        constraint pk_ta_dia_docto primary key (cod_docto),
        constraint fk_ta_dia_docto_usrio foreign key (cod_usrio) references dbo.ta_dia_mst_mov_usrio (cod_usrio),
        constraint fk_ta_dia_docto_tpo foreign key (cod_tpo_docto) references dbo.ta_dia_mst_mov_tpo_docto (cod_tpo_docto),
        constraint fk_ta_dia_docto_estdo foreign key (cod_estdo_docto) references dbo.ta_dia_mst_estdo_docto (cod_estdo_docto)
    );
go

if not exists (select 1 from sys.indexes where name = 'ix_ta_dia_docto_usrio')
    create index ix_ta_dia_docto_usrio on dbo.ta_dia_docto (cod_usrio, cnr_fec_rgtro);
go

if object_id('dbo.ta_dia_docto_cntnd', 'u') is null
    create table dbo.ta_dia_docto_cntnd (
        cod_docto       int            not null,
        bin_cntnd       varbinary(max) not null,
        cnr_fec_rgtro   datetime       not null,
        cnr_usr_rgtro   int            not null,
        cnr_fec_mdfcn   datetime       null,
        cnr_usr_mdfcn   int            null,
        cnr_estdo_rgtro int            not null,
        constraint pk_ta_dia_docto_cntnd primary key (cod_docto),
        constraint fk_ta_dia_docto_cntnd_docto foreign key (cod_docto) references dbo.ta_dia_docto (cod_docto)
    );
go

if object_id('dbo.ta_dia_docto_campo', 'u') is null
    create table dbo.ta_dia_docto_campo (
        cod_docto       int           not null,
        cod_campo       int           not null,
        nom_clave       varchar(60)   not null,
        nom_etqta       varchar(100)  not null,
        dsc_valor       varchar(1000) not null,
        dsc_valor_cnfrm varchar(1000) null,
        num_cnfnz       decimal(4, 3) not null,
        cnr_fec_rgtro   datetime      not null,
        cnr_usr_rgtro   int           not null,
        cnr_fec_mdfcn   datetime      null,
        cnr_usr_mdfcn   int           null,
        cnr_estdo_rgtro int           not null,
        constraint pk_ta_dia_docto_campo primary key (cod_docto, cod_campo),
        constraint fk_ta_dia_docto_campo_docto foreign key (cod_docto) references dbo.ta_dia_docto (cod_docto)
    );
go

if object_id('dbo.ta_dia_docto_mnsje', 'u') is null
    create table dbo.ta_dia_docto_mnsje (
        cod_mnsje       int          not null,
        cod_docto       int          not null,
        cod_orgen_mnsje int          not null,
        dsc_texto       varchar(max) not null,
        cnr_fec_rgtro   datetime     not null,
        cnr_usr_rgtro   int          not null,
        cnr_fec_mdfcn   datetime     null,
        cnr_usr_mdfcn   int          null,
        cnr_estdo_rgtro int          not null,
        constraint pk_ta_dia_docto_mnsje primary key (cod_mnsje),
        constraint fk_ta_dia_docto_mnsje_docto foreign key (cod_docto) references dbo.ta_dia_docto (cod_docto),
        constraint fk_ta_dia_docto_mnsje_orgen foreign key (cod_orgen_mnsje) references dbo.ta_dia_mst_orgen_mnsje (cod_orgen_mnsje)
    );
go

if not exists (select 1 from sys.indexes where name = 'ix_ta_dia_docto_mnsje_docto')
    create index ix_ta_dia_docto_mnsje_docto on dbo.ta_dia_docto_mnsje (cod_docto, cod_mnsje);
go
