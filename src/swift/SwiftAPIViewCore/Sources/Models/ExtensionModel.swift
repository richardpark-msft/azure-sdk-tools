// --------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// The MIT License (MIT)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the ""Software""), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//
// --------------------------------------------------------------------------

import Foundation
import SwiftSyntax


class ExtensionModel: Tokenizable, Linkable {
    var accessLevel: AccessLevel
    var extendedType: String
    var name: String
    var definitionId: String?
    // treat extensions as if they have no parents
    var parent: Linkable? = nil
    var members: [DeclarationModel]
    let childNodes: SyntaxChildren
    private let decl: ExtensionDeclSyntax

    /// Initialize from initializer declaration
    init(from decl: ExtensionDeclSyntax) {
        self.accessLevel = decl.modifiers.accessLevel
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        let extType = decl.extendedType
        switch extType.kind {
        case .simpleTypeIdentifier:
            self.extendedType = SimpleTypeIdentifierSyntax(extType)!.name.withoutTrivia().description
        case .memberTypeIdentifier:
            self.extendedType = MemberTypeIdentifierSyntax(extType)!.name.withoutTrivia().description
        default:
            SharedLogger.warn("Unhandled extended type kind: \(extType.kind). APIView may not display correctly")
            self.extendedType = "_UNKNOWN_"
        }
        self.members = [DeclarationModel]()
        self.decl = decl
        self.definitionId = nil
        self.name = ""
    }

    private func identifier(for decl: ExtensionDeclSyntax) -> String {
        var defId = ""
        var modifiedDecl = decl.withoutTrivia()
        modifiedDecl.attributes = nil
        modifiedDecl.modifiers = nil
        defId = modifiedDecl.withoutTrivia().description
        defId = defId.filter { !$0.isWhitespace }
        // remove the member block
        if let idx = defId.firstIndex(of: "{") {
            defId.removeSubrange(idx..<defId.endIndex)
        }
        return defId
    }

    func processMembers() {
        self.definitionId = identifier(for: decl)
        for member in decl.members.members {
            let decl = member.decl
            switch decl.kind {
                case .actorDecl:
                    appendIfVisible(DeclarationModel(from: ActorDeclSyntax(decl)!, parent: self))
                case .classDecl:
                    appendIfVisible(DeclarationModel(from: ClassDeclSyntax(decl)!, parent: self))
                case .deinitializerDecl:
                    // deinitializers cannot be called by users, so it makes no sense
                    // to expose them in APIView
                    break
                case .enumDecl:
                    appendIfVisible(DeclarationModel(from: EnumDeclSyntax(decl)!, parent: self))
                case .functionDecl:
                    appendIfVisible(DeclarationModel(from: FunctionDeclSyntax(decl)!, parent: self))
                case .importDecl:
                    // purposely ignore import declarations
                    break
                case .operatorDecl:
                    let model = DeclarationModel(from: OperatorDeclSyntax(decl)!, parent: self)
                    // operators are global and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .precedenceGroupDecl:
                    let model = DeclarationModel(from: PrecedenceGroupDeclSyntax(decl)!, parent: self)
                    // precedence groups are public and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .protocolDecl:
                    appendIfVisible(DeclarationModel(from: ProtocolDeclSyntax(decl)!, parent:  self))
                case .structDecl:
                    appendIfVisible(DeclarationModel(from: StructDeclSyntax(decl)!, parent: self))
                case .typealiasDecl:
                    appendIfVisible(DeclarationModel(from: TypealiasDeclSyntax(decl)!, parent: self))
                case .extensionDecl:
                    SharedLogger.warn("Extensions containing extensions is not supported. Contact the Swift APIView team.")
                case .initializerDecl:
                    appendIfVisible(DeclarationModel(from: InitializerDeclSyntax(decl)!, parent: self))
                case .subscriptDecl:
                    appendIfVisible(DeclarationModel(from: SubscriptDeclSyntax(decl)!, parent: self))
                case .variableDecl:
                    appendIfVisible(DeclarationModel(from: VariableDeclSyntax(decl)!, parent: self))
                default:
                    // Create an generic declaration model of unknown type
                    appendIfVisible(DeclarationModel(from: decl, parent: self))
                }
        }
    }

    func appendIfVisible(_ decl: DeclarationModel) {
        let publicModifiers = CodeModel.publicModifiers
        if publicModifiers.contains(decl.accessLevel) || publicModifiers.contains(self.accessLevel) {
            self.members.append(decl)
        }
    }

    func tokenize(apiview a: CodeModel, parent: Linkable?) {
        for child in childNodes {
            var options = ReviewTokenOptions()
            let childIdx = child.indexInParent
            if childIdx == 7 {
                child.tokenize(apiview: a, parent: parent)
                if let last = a.currentLine.tokens.popLast() {
                    // These are made as type references, but they should be
                    // type declarations
                    a.currentLine.lineId = self.definitionId
                    last.navigateToId = self.definitionId
                    a.currentLine.tokens.append(last)
                }
            } else if childIdx == 13 {
                // special case for extension members
                options.applySpacing(SwiftSyntax.TokenKind.leftBrace.spacing)
                a.punctuation("{", options: options)
                if !members.isEmpty {
                    a.indent {
                        for member in members {
                            member.tokenize(apiview: a, parent: parent)
                            a.blankLines(set: 0)
                        }
                    }
                }
                a.blankLines(set: 0)
                options.applySpacing(SwiftSyntax.TokenKind.rightBrace.spacing)
                a.punctuation("}", options: options)
                a.newline()
            } else {
                child.tokenize(apiview: a, parent: parent)
            }
        }
    }
}

extension Array<ExtensionModel> {
    func tokenize(apiview a: CodeModel, parent: Linkable?) {
        a.blankLines(set: 1)
        let lastIdx = self.count - 1
        for (idx, ext) in self.enumerated() {
            ext.tokenize(apiview: a, parent: parent)
            if idx != lastIdx {
                a.blankLines(set: 1)
            }
        }
    }

    func resolveDuplicates() -> [ExtensionModel] {
        var resolved = [String: ExtensionModel]()
        for ext in self {
            guard let defId = ext.definitionId else {
                fatalError("No definition ID found for extension!")
            }
            if let match = resolved[defId] {
                let resolvedMembers = Dictionary(uniqueKeysWithValues: match.members.map { ($0.definitionId, $0) } )
                for member in ext.members {
                    if resolvedMembers[member.definitionId] != nil {
                        continue
                    } else {
                        match.members.append(member)
                    }
                }
            } else {
                resolved[defId] = ext
            }
        }
        return Array(resolved.values).sorted(by: {$0.definitionId! < $1.definitionId! })
    }
}
