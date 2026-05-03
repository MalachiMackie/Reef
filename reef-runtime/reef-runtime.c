#include <assert.h>
#include <corecrt_search.h>
#include <stdalign.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <stdbool.h>
#include <intrin.h>
#include <string.h>

#define STB_DS_IMPLEMENTATION
#include "stb_ds.h"

#define HT_IMPLEMENTATION
#include "ht.h"

#pragma intrinsic(_ReturnAddress)

#ifdef _MSC_VER
typedef double max_align_t;
#endif

#ifdef __GNUC__
#define PACK( __Declaration__ ) __Declaration__ __attribute__((__packed__))
#endif

#ifdef _MSC_VER
#define PACK( __Declaration__ ) __pragma( pack(push, 1) ) __Declaration__ __pragma( pack(pop))
#endif

void print_i8(int8_t num);
void print_i16(int16_t num);
void print_i32(int32_t num);
void print_i64(int64_t num);

void print_u8(uint8_t num);
void print_u16(uint16_t num);
void print_u32(uint32_t num);
void print_u64(uint64_t num);

typedef uint32_t TypeId;

typedef PACK(struct {
    TypeId typeId;
}) ObjectHeader;

typedef PACK(struct {
	uint64_t length;
	char chars[];
}) string;

typedef PACK(struct {
    ObjectHeader objectHeader;
    char padding[4];
    string str;
}) StringBoxedValue;

typedef PACK(struct {
    StringBoxedValue *original_string;
    uint64_t offset;
    uint64_t length;
}) string_slice;

typedef PACK(struct {
    uint16_t variantIdentifier;
    int16_t offset;
}) VariablePlaceOffsetVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    int16_t offset;
}) VariablePlacePointerVariant;

typedef PACK(struct {
    union {
        uint16_t variantIdentifier;
        VariablePlaceOffsetVariant offsetInfo;
        VariablePlacePointerVariant pointerInfo;
    };
}) VariablePlace;

typedef PACK(struct {
    TypeId typeId;
    VariablePlace place;
    StringBoxedValue *name;
}) MethodParameter;

typedef PACK(struct {
    uint64_t length;
    MethodParameter items[];
}) MethodParameterArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    MethodParameterArray items;
}) MethodParameterArrayBoxedValue;

typedef PACK(struct {
    StringBoxedValue *name;
    TypeId typeId;
    uint16_t offset;
    uint16_t _padding;
}) FieldInfo;

typedef PACK(struct {
    StringBoxedValue *name;
    TypeId typeId;
    uint32_t _padding;
}) StaticFieldInfo;

typedef PACK(struct {
    uint64_t length;
    FieldInfo items[];
}) FieldInfoArray;

typedef PACK(struct {
    uint64_t length;
    StaticFieldInfo items[];
}) StaticFieldInfoArray;

typedef PACK(struct {
    TypeId typeId;
    uint32_t _padding;
    FieldInfoArray items;
}) FieldInfoArrayBoxedValue;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    StaticFieldInfoArray items;
}) StaticFieldInfoArrayBoxedValue;

typedef PACK(struct {
    StringBoxedValue *name;
    FieldInfoArrayBoxedValue *fields;
    bool containsPointer;
    char _padding[7];
}) VariantInfo;

typedef PACK(struct {
    uint64_t length;
    VariantInfo items[];
}) VariantInfoArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    VariantInfoArray items;
}) VariantInfoArrayBoxedValue;

typedef PACK(struct {
    uint16_t variantIdentifier;
    char _padding[6];
    StringBoxedValue *fullyQualifiedName;
    StringBoxedValue *name;
    uint64_t size;
    TypeId typeId;
    char __padding[4];
    StaticFieldInfoArrayBoxedValue *staticFields;
    FieldInfoArrayBoxedValue *fields;
    bool containsPointer;
    char ___padding[7];
}) TypeInfoClassVariant;

typedef uint16_t (*get_variant_identifier_fn)(void* data);

typedef PACK(struct {
    uint16_t variantIdentifier;
    char _padding[6];
    StringBoxedValue *fullyQualifiedName;
    StringBoxedValue *name;
    uint64_t size;
    TypeId typeId;
    char __padding[4];
    StaticFieldInfoArrayBoxedValue *staticFields;
    VariantInfoArrayBoxedValue *variants;
    struct {
        get_variant_identifier_fn functionReference;
        void* functionParameter;
    } variantIdentifierGetter;
    bool containsPointer;
    char ___padding[7];
}) TypeInfoUnionVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    char _padding[6];
    StringBoxedValue *fullyQualifiedName;
    TypeId pointerToTypeId;
    char __padding[4];
}) TypeInfoPointerVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    char _padding[6];
    StringBoxedValue *fullyQualifiedName;
    TypeId elementTypeId;
    char __padding[4];
    uint64_t length;
    bool isDynamic;
    char ___padding[7];
}) TypeInfoArrayVariant;

typedef PACK(struct {
    union {
        uint16_t variantIdentifier;
        TypeInfoClassVariant classInfo;
        TypeInfoUnionVariant unionInfo;
        TypeInfoPointerVariant pointerInfo;
        TypeInfoArrayVariant arrayInfo;
    };
}) TypeInfo;

extern TypeInfo typeInfoArray[];

typedef PACK(struct {
    uint64_t length;
    TypeId typeIds[];
}) TypeIdArray;

typedef PACK(struct {
    TypeId typeId;
    int16_t stackOffset;
    uint16_t _padding;
}) MethodLocal;

typedef PACK(struct {
    uint64_t length;
    MethodLocal locals[];
}) MethodLocalArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    MethodLocalArray locals;
}) LocalsBoxedValue;

typedef PACK(struct {
    uint32_t methodId;
    uint32_t _padding;
    StringBoxedValue *fullyQualifiedName;
    StringBoxedValue *name;
    MethodParameterArrayBoxedValue *parameters;
    LocalsBoxedValue *locals;
    uint64_t addressFrom;
    uint64_t addressTo;
}) MethodInfo;

extern MethodInfo methodInfoArray[];
extern uint64_t methodInfoCount;
extern uint64_t methodInfoSize;
extern uint64_t typeInfoCount;
extern uint64_t typeInfoSize;
extern uint64_t fieldInfoSize;
extern uint64_t variantInfoSize;
extern TypeId boxedValueStringTypeId;

MethodInfo *get_method_info(uint64_t index)
{
    assert(index < methodInfoCount);
    return &methodInfoArray[index];
}

TypeInfo *get_type_info(uint64_t index)
{
    assert(index < typeInfoCount);

    return &typeInfoArray[index];
}

void print_string_slice(string_slice slice) {
    if (slice.original_string->str.length - slice.offset == slice.length)
    {
        // slice ends at the end of the original string, which is null terminated, so just print using fputs
        fputs(slice.original_string->str.chars + slice.offset, stdout);
        return;
    }

    for (uint64_t i = slice.offset; i < slice.offset + slice.length; i++) {
        fputc(slice.original_string->str.chars[i], stdout);
    }
}

void print_string(StringBoxedValue *str)
{
    // strings are always null terminated
    fputs(str->str.chars, stdout);
}

#define DEFINE_PRINT_INT(func_name, int_type)                            \
void func_name(int_type num)                                             \
{                                                                        \
    /* Handle negative numbers safely */                                 \
    if (num < 0)                                                         \
    {                                                                    \
        fputc('-', stdout);                                              \
                                                                         \
        /* Convert to positive without overflow */                       \
        int_type n = -(num + 1);                                         \
        n += 1;                                                          \
        num = n;                                                         \
    }                                                                    \
                                                                         \
    if (num > 9)                                                         \
    {                                                                    \
        int_type a = num / 10;                                           \
        num -= a * 10;                                                   \
        func_name(a);                                                    \
    }                                                                    \
                                                                         \
    fputc('0' + (int)num, stdout);                                       \
}

DEFINE_PRINT_INT(print_i8, int8_t)
DEFINE_PRINT_INT(print_i16, int16_t)
DEFINE_PRINT_INT(print_i32, int32_t)
DEFINE_PRINT_INT(print_i64, int64_t)

DEFINE_PRINT_INT(print_u8, uint8_t)
DEFINE_PRINT_INT(print_u16, uint16_t)
DEFINE_PRINT_INT(print_u32, uint32_t)
DEFINE_PRINT_INT(print_u64, uint64_t)

void print_size_t_hex(size_t value)
{
    int started = 0;

    for (int i = (int)(sizeof(size_t) * 2) - 1; i >= 0; --i)
    {
        unsigned digit = (value >> (i * 4)) & 0xF;

        if (digit != 0 || started || i == 0)
        {
            started = 1;
            putchar(digit < 10 ? '0' + digit : 'A' + (digit - 10));
        }
    }
}

void print_method_info(MethodInfo* handle)
{
    print_string(handle->fullyQualifiedName);
    fputs(":\n    id: ", stdout);
    print_u32(handle->methodId);

    fputs("\n    display_name: ", stdout);
    print_string(handle->name);

    fputs("\n    address_from: 0x", stdout);
    print_size_t_hex(handle->addressFrom);

    fputs("\n    address_to: 0x", stdout);
    print_size_t_hex(handle->addressTo);

    fputs("\n    parameters: \n            length: ", stdout);
    MethodParameterArray* parameters = &handle->parameters->items;
    print_u64(parameters->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < parameters->length; i++)
    {
        if (i > 0)
        {
            fputs("\n", stdout);
        }
        MethodParameter parameter = parameters->items[i];
        fputs("            - name: ", stdout);
        print_string(parameter.name);
        fputs("\n              typeId: ", stdout);
        print_u32(parameter.typeId);
        fputs("\n              place:\n                variant: ", stdout);
        switch (parameter.place.variantIdentifier)
        {
            case 0:
                fputs("Offset", stdout);
                fputs("\n                offset: ", stdout);
                print_u16(parameter.place.offsetInfo.offset);
                break;
            case 1:
                fputs("Pointer", stdout);
                fputs("\n                pointerPlace: ", stdout);
                print_u16(parameter.place.pointerInfo.offset);
                break;
            default:
                assert(0);
                break;
        }
    }
    fputs("\n    locals: \n            length: ", stdout);
    MethodLocalArray* locals = &handle->locals->locals;
    print_u64(locals->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < locals->length; i++)
    {
        if (i > 0)
        {
            fputs("\n", stdout);
        }
        fputs("            - typeId: ", stdout);
        print_u32(locals->locals[i].typeId);
        fputs("\n              offset: ", stdout);
        print_i16(locals->locals[i].stackOffset);
    }
    fputs("\n", stdout);
}

void print_type_info(TypeInfo *handle)
{
    switch (handle->variantIdentifier)
    {
        case 0: // class
        {
            print_string(handle->classInfo.fullyQualifiedName);
            fputs(":\n    type: class\n    id: ", stdout);
            print_u32(handle->classInfo.typeId);
            fputs("\n    displayName: ", stdout);
            print_string(handle->classInfo.name);
            uint64_t fields_count = handle->classInfo.fields->items.length;
            fputs("\n    containsPointer: ", stdout);
            fputs(handle->classInfo.containsPointer ? "true" : "false", stdout);
            fputs("\n    fields:\n        length: ", stdout);
            print_u64(fields_count);
            fputs("\n        items:", stdout);
            for (uint64_t i = 0; i < fields_count; i++)
            {
                if (i > 0)
                {
                    fputs("\n", stdout);
                }
                FieldInfo field = handle->classInfo.fields->items.items[i];
                fputs("\n            - name: ", stdout);
                print_string(field.name);
                fputs("\n              type_id: ", stdout);
                print_u32(field.typeId);
                fputs("\n              offset: ", stdout);
                print_u16(field.offset);
            }
            fputs("\n", stdout);
            break;
        }
        case 1: // union
        {
            print_string(handle->unionInfo.fullyQualifiedName);
            fputs(":\n    type: union\n    id: ", stdout);
            print_u32(handle->unionInfo.typeId);
            fputs("\n    displayName: ", stdout);
            print_string(handle->unionInfo.name);
            fputs("\n    containsPointer: ", stdout);
            fputs(handle->unionInfo.containsPointer ? "true" : "false", stdout);
            fputs("\n    variants_count: ", stdout);
            print_u64(handle->unionInfo.variants->items.length);
            fputs("\n", stdout);
            break;
        }
        case 2: // pointer
        {
            print_string(handle->pointerInfo.fullyQualifiedName);
            fputs(":\n    type: pointer\n", stdout);
            break;
        }
        case 3: // array
        {
            print_string(handle->arrayInfo.fullyQualifiedName);
            fputs(":\n    type: array\n    is_dynamic: ", stdout);
            if (!handle->arrayInfo.isDynamic)
            {
                fputs("false", stdout);
            }
            else
            {
                fputs("true", stdout);
            }
            fputs("\n", stdout);
            break;
        }
    }
}

size_t memory_used_bytes = 0;

size_t heap_size_bytes = 1024 * 10; // 10MB

typedef struct {
    // start of the actual allocation, may not be aligned to alignof(max_align_t). Will be at most alignof(max_align_t) smaller than ptr
    uint64_t start;

    // the address of the data. Returned to reef code. guaranteed to be aligned to alignof(max_align_t)
    uint64_t ptr;

    // Size of the allocation from start. Includes any alignment bytes from start but before ptr
    uint64_t size;
} Allocation;

typedef Ht(uint64_t, Allocation) AllocationHashTable;

typedef struct {
    uint64_t ptr;
    uint64_t size;
} HeapGap;

typedef struct {
    uint64_t base_addr;
    uint64_t size;
    AllocationHashTable allocations;
    HeapGap *gaps;
} Heap;

Heap* heaps;

void init_runtime() {
    assert(typeInfoSize == sizeof(TypeInfo));
    assert(methodInfoSize == sizeof(MethodInfo));
}

void* allocate_inside_heap(Heap *heap, uint64_t size) {
    for (uint64_t j = 0; j < arrlenu(heap->gaps); j++)
    {
        HeapGap *gap = &heap->gaps[j];
        if (size > gap->size)
        {
            // gap too small
            continue;
        }
        uint64_t gapAlignmentBytes = gap->ptr % alignof(max_align_t);
        if (gapAlignmentBytes > 0)
        {
            gapAlignmentBytes = alignof(max_align_t) - gapAlignmentBytes;
        }

        Allocation allocation = {
            .start = gap->ptr,
            .ptr = gap->ptr + gapAlignmentBytes,
            .size = size + gapAlignmentBytes
        };
        memory_used_bytes += size;
        *ht_put(&heap->allocations, allocation.ptr) = allocation;

        if (size + gapAlignmentBytes == gap->size)
        {
            // allocation takes up the whole gap, remove it
            arrdel(heap->gaps, j);

            return (void*)allocation.ptr;
        }

        // allocation is smaller than the gap, so shrink it
        gap->ptr += size + gapAlignmentBytes;
        gap->size -= size + gapAlignmentBytes;
        return (void*)allocation.ptr;
    }

    return NULL;
}

void* allocate(uint64_t size) {
    void* ptr = NULL;
    for (uint64_t i = 0; i < arrlenu(heaps); i++)
    {
        ptr = allocate_inside_heap(&heaps[i], size);
        if (ptr)
        {
            return ptr;
        }
    }

    // if we got here, then there was no space in any of the heaps. So need to allocate a new one

    // todo: try compacting heaps before allocating a new one

    uint64_t heap_size = size <= heap_size_bytes ? heap_size_bytes : size;

    Heap newHeap = {
        .base_addr = (uint64_t)malloc(heap_size),
        .allocations = {0},
        .gaps = NULL,
        .size = heap_size_bytes
    };
    assert(newHeap.base_addr);

    HeapGap initialGap = {
        .ptr = newHeap.base_addr,
        .size = newHeap.size
    };
    arrput(newHeap.gaps, initialGap);
    arrput(heaps, newHeap);

    ptr = allocate_inside_heap(&heaps[arrlenu(heaps) - 1], size);
    assert(ptr);

    return ptr;
}

StringBoxedValue *allocate_new_string(uint64_t length) {
    assert(length > 0);

    uint64_t size =
        length
        + 1 // extra byte for null terminated string that it outside of length
        + sizeof(uint64_t) // length
        + sizeof(ObjectHeader);

    // for now this assumes 1 byte characters. Once we support unicode characters, this will need to change
    StringBoxedValue *result = allocate(size);
    result->objectHeader.typeId = boxedValueStringTypeId;
    result->str.length = length;
    result->str.chars[length] = 0; // ensure string is null terminated

    return result;
}

void copy_string(StringBoxedValue *destination, StringBoxedValue *source, uint64_t sourceOffset)
{
    assert(source->str.length - sourceOffset >= destination->str.length);

    memcpy_s(
        destination->str.chars,
        destination->str.length,
        source->str.chars + sourceOffset,
        destination->str.length
    );
}

void print_all_types()
{
    for (uint64_t i = 0; i < typeInfoCount; i++)
    {
        print_type_info(get_type_info(i));
    }
}

void print_all_methods()
{
    for (uint64_t i = 0; i < methodInfoCount; i++)
    {
        print_method_info(get_method_info(i));
    }
}

extern void* get_rbp();

MethodInfo* try_get_reef_method_by_instruction_address(uint64_t address)
{
    for (uint32_t i = 0; i < methodInfoCount; i++)
    {
        MethodInfo* method = &methodInfoArray[i];
        if (address > method->addressFrom && address < method->addressTo)
        {
            return method;
        }
    }

    return NULL;
}

typedef void(*traverse_stack_fn)(uint16_t depth, uint64_t stackBaseAddress, uint64_t instructionAddress, MethodInfo* method, void* data);

void traverse_stack(traverse_stack_fn fn, uint64_t rbp, uint64_t firstReturnAddress, void* data)
{
    uint64_t* nextRbp = (uint64_t*)rbp;

    uint64_t returnAddress = firstReturnAddress;
    MethodInfo* reefMethod = try_get_reef_method_by_instruction_address(returnAddress);

    uint16_t depth = 0;
    while (reefMethod != NULL)
    {
        fn(depth, (uint64_t)nextRbp, returnAddress, reefMethod, data);
        nextRbp = (uint64_t*)*nextRbp;
        returnAddress = *(nextRbp + 1);
        reefMethod = try_get_reef_method_by_instruction_address(returnAddress);
        depth++;
    }
}

void print_method(uint16_t depth, uint64_t stackBaseAddress, uint64_t instructionAddress, MethodInfo* method, void* data)
{
    if (depth > 0)
    {
        fputs("\n", stdout);
    }
    print_string(method->fullyQualifiedName);
}

void print_stack_trace()
{
    uint64_t rbp = (uint64_t)get_rbp();
    uint64_t returnAddress = (uint64_t)_ReturnAddress();
    traverse_stack(&print_method, rbp, returnAddress, NULL);
}

bool type_info_has_pointer(TypeInfo *type)
{
    switch (type->variantIdentifier)
    {
        case 0: // class
            return type->classInfo.containsPointer;
        case 1: // union
            return type->unionInfo.containsPointer;
        case 2: // pointer
            return true;
        case 3: // array
            TypeInfo* elementType = &typeInfoArray[type->arrayInfo.elementTypeId];
            return type_info_has_pointer(elementType);
    }
}

uint64_t get_type_size(TypeInfo *type)
{
    switch (type->variantIdentifier)
    {
        case 0: // class
            return type->classInfo.size;
        case 1: // union
            return type->unionInfo.size;
        case 2: // pointer
            return 8;
        case 3: // array
            TypeInfo* elementType = &typeInfoArray[type->arrayInfo.elementTypeId];
            return 8 + get_type_size(elementType);
            // return type_info_has_pointer(elementType);
    }
    assert(false);
}

typedef Ht(uint64_t, bool) AlivePointerHashTable;

void check_type_references(TypeInfo *type, uint64_t valueAddress, AlivePointerHashTable *alive_pointers)
{
    switch (type->variantIdentifier)
    {
        case 0: // class
            if (!type->classInfo.containsPointer)
            {
                return;
            }
            FieldInfoArray* fieldInfoArray = &type->classInfo.fields->items;
            for (uint64_t i = 0; i < fieldInfoArray->length; i++)
            {
                FieldInfo *fieldInfo = &fieldInfoArray->items[i];
                TypeInfo *fieldTypeInfo = &typeInfoArray[fieldInfo->typeId];
                uint64_t fieldAddress = (uint64_t)((char*)valueAddress + fieldInfo->offset);

                check_type_references(fieldTypeInfo, fieldAddress, alive_pointers);
            }
            break;
        case 1: // union
            if (!type->unionInfo.containsPointer)
            {
                return;
            }

            uint16_t variantIdentifier = type->unionInfo.variantIdentifierGetter.functionReference((void*)valueAddress);

            VariantInfoArray *variantInfoArray = &type->unionInfo.variants->items;
            VariantInfo *variant = &variantInfoArray->items[variantIdentifier];
            if (!variant->containsPointer)
            {
                return;
            }

            FieldInfoArray *unionFinfoArray = &variant->fields->items;
            for (uint64_t j = 0; j < unionFinfoArray->length; j++)
            {
                FieldInfo *fieldInfo = &unionFinfoArray->items[j];

                TypeInfo *fieldTypeInfo = &typeInfoArray[fieldInfo->typeId];
                uint64_t fieldAddress = (uint64_t)((char*)valueAddress + fieldInfo->offset);

                check_type_references(fieldTypeInfo, fieldAddress, alive_pointers);
            }

            break;
        case 2: // pointer
            uint64_t ptrValue = *(uint64_t*)valueAddress;

            // we've already found this pointer. It must be a circular reference, so break the cycle here
            if (ht_find(alive_pointers, ptrValue))
            {
                return;
            }

            *ht_put(alive_pointers, ptrValue) = true;

            TypeInfo* pointerToType = &typeInfoArray[type->pointerInfo.pointerToTypeId];

            check_type_references(pointerToType, ptrValue, alive_pointers);
            break;
        case 3: // array
            TypeInfo* elementType = &typeInfoArray[type->arrayInfo.elementTypeId];
            uint64_t elementSize = get_type_size(elementType);

            if (!type_info_has_pointer(elementType))
            {
                return;
            }

            char* firstElement = (char*)((uint64_t *)valueAddress + 1);
            uint64_t *length = (uint64_t*)valueAddress;

            for (uint64_t i = 0; i < *length; i++)
            {
                uint64_t elementAddress = (uint64_t)(firstElement + elementSize * i);
                check_type_references(elementType, elementAddress, alive_pointers);
            }

            break;
    }
}

void check_method_references(
    uint16_t depth,
    uint64_t stackBaseAddress,
    uint64_t instructionAddress,
    MethodInfo* method,
    void* data)
{
    for (uint64_t i = 0; i < method->locals->locals.length; i++)
    {
        MethodLocal *local = &method->locals->locals.locals[i];
        void* localAddress = (((char*)stackBaseAddress) + local->stackOffset);
        TypeInfo* type = &typeInfoArray[local->typeId];

        check_type_references(type, (uint64_t)localAddress, data);
    }
}

typedef struct {
    Allocation *allocation;
    Heap *heap;
} DeadAllocation;

AlivePointerHashTable alive_pointers = {0};
DeadAllocation* dead_allocations = NULL;

void trigger_gc()
{
    uint64_t rbp = (uint64_t)get_rbp();
    uint64_t returnAddress = (uint64_t)_ReturnAddress();
    traverse_stack(&check_method_references, rbp, returnAddress, &alive_pointers);

    for (uint64_t i = 0; i < arrlenu(heaps); i++)
    {
        Heap *heap = &heaps[i];
        ht_foreach(value, &heap->allocations) {
            uint64_t address = ht_key(&heap->allocations, value);

            if (ht_find(&alive_pointers, address))
            {
                // allocation is still alive
                continue;
            }

            DeadAllocation dead_allocation = {
                .allocation = value,
                .heap = heap
            };

            arrput(dead_allocations, dead_allocation);
        }

        for (uint64_t i = 0; i < arrlenu(dead_allocations); i++)
        {
            DeadAllocation dead_allocation = dead_allocations[i];
            memory_used_bytes -= dead_allocation.allocation->size;
            Heap *heap = dead_allocation.heap;

            if (heap->allocations.count == 1)
            {
                // this is the only allocation within the heap, clear it out and continue on. Fast path
                ht_reset(&heap->allocations);
                arrsetlen(heap->gaps, 0);
                HeapGap newGap = {
                    .ptr = heap->base_addr,
                    .size = heap->size
                };
                arrput(heap->gaps, newGap);
                continue;
            }

            uint64_t start = dead_allocation.allocation->start;

            bool gap_updated = false;
            for (uint64_t j = 0; j < arrlenu(heap->gaps); j++)
            {
                HeapGap *gap = &heap->gaps[j];

                if (start < gap->ptr)
                {
                    if (start + dead_allocation.allocation->size == gap->ptr)
                    {
                        // allocation is before this gap and is on the boundry, so just expand the gap
                        gap->ptr = start;
                        gap->size += dead_allocation.allocation->size;
                    }
                    else
                    {
                        // allocation is before this gap but is not on the boundry, so insert before it
                        HeapGap newGap = {
                            .ptr = start,
                            .size = dead_allocation.allocation->size
                        };
                        arrins(heap->gaps, j, newGap);
                    }
                    gap_updated = true;
                    break;
                }
            }

            if (!gap_updated)
            {
                // the allocation is after the last gap
                HeapGap newGap = {
                    .ptr = start,
                    .size = dead_allocation.allocation->size
                };
                arrput(heap->gaps, newGap);
            }

            ht_delete(&heap->allocations, dead_allocation.allocation);
        }
    }

    // don't free, just reset so we can reuse again
    ht_reset(&alive_pointers);
    arrsetlen(dead_allocations, 0);
}

size_t get_memory_usage_bytes() {
    return memory_used_bytes;
}
