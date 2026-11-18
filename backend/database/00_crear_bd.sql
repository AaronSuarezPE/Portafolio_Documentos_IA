/*
    Crea la base de datos local de DocuIA.
    Solo para desarrollo: en el hosting la base ya viene creada por el proveedor.
*/
if db_id('docuia') is null
    create database docuia;
go
